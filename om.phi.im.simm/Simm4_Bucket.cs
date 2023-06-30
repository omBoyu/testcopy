//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm4_Bucket : NodeMargin
    {
        public Simm4_Bucket(List<SimmRiskFactor> loRiskFactors, string nodeName, SimmRiskClassType riskClass, SimmIMClassType iMComputationType )
            : base(loRiskFactors, nodeName, riskClass, iMComputationType, SimmNodeMarginType.Bucket)
        {
            GenerateChildren();
        }

        public override void GenerateChildren()
        {
            if (LoRiskFactors.Count > 0)
            {
                var subBuckets = LoRiskFactors.GroupBy(rf => rf.SubBucket);// do Sub Buckets
                int nSubBuckets = subBuckets.Count();

                int nSubBucket = 0;
                //TODO: could be optimized - not computing sums for each subbuckets (IR and creds) but at the grouping level, than sending tasks
                foreach (var subBucket in subBuckets)
                {
                    List<SimmRiskFactor> subBucketRFs = subBucket.ToList();// if RiskClass = "FX" --> subbucket = CCY

                    SimmRiskFactor rf = subBucketRFs[0];// just to get 1

                    double sensiTotalforConcentration = 0;

                    if (IMClassEnum == SimmIMClassType.DeltaIM || IMClassEnum == SimmIMClassType.VegaIM)// concentrations only for DeltaIM and VegaIM
                    {
                        sensiTotalforConcentration =
                            
                            // getting total for IR at CCy level - a.k.a bucket
                            (rf.Enum4RiskClass == SimmRiskClassType.IR) ? 
                                LoRiskFactors.Where(x => ((x.Qualifier == rf.Qualifier) && (x.RiskTypeEnum != SimmRiskTypeType.Risk_XCcyBasis))).Sum(w => w.AmountUSD) :
                            
                                // getting total for CR & NQCR at Issuer & seniority level (bucket is seniority and Issuer is Isin)
                                ((rf.Enum4RiskClass == SimmRiskClassType.CR) || (rf.Enum4RiskClass == SimmRiskClassType.CRNQ)) ? 
                                    LoRiskFactors.Where(x => x.Qualifier == rf.Qualifier).Sum(y => y.AmountUSD) :
                                
                                    // not used for EQ, COM and FX
                                    0;
                    }

                    Simm5_SubBucket marginElement = new Simm5_SubBucket(subBucketRFs, subBucket.Key, RiskClassEnum, IMClassEnum, sensiTotalforConcentration, NodeName);// NodeName is RealBucketName, it is the ParentNodeName
                    Children.Add(marginElement);

                    nSubBucket++;
                }
            }
        }

        public override void NodeCompute()
        {
            // Compute Children
            foreach (var child in Children)
                child.NodeCompute();


            switch (IMClassEnum)
            {
                case SimmIMClassType.DeltaIM:
                    ComputeDeltaIM();
                    break;

                case SimmIMClassType.VegaIM:
                    ComputeVegaIM();
                    break;

                case SimmIMClassType.CurvIM:
                    ComputeCurvIM();
                    break;

                case SimmIMClassType.BaseCorrIM:
                    ComputeBaseCorrIM();
                    break;

                case SimmIMClassType.None: throw new Exception("IMClassEnum is NullCustom");

                default: throw new Exception("No IMComputationType");
            }

            //// we need to reduce after computation
            //ReduceChildren();
        }

        void ComputeDeltaIM()
        {
            if (Children.Count == 0)
            {
                ChildrenTotalSensi = 0; // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = 0;
                Margin = 0;
            }
            else if (Children.Count == 1)
            {
                ChildrenTotalSensi = Children[0].Margin; // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = Math.Abs(Children[0].Margin);
                Margin = Math.Abs(Children[0].Margin);// squareRoot of square
            }
            else //if (Children.Count > 1)
            {
                ChildrenTotalSensi = Children.Sum(c => c.Margin); // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = Children.Sum(c => Math.Abs(c.Margin));

                if (RiskClassEnum == SimmRiskClassType.IR)//  "Interest Rate" 
                {
                    double KSquared = 0;
                    double PHIij = 0;
                    double RHOkl = 0;
                    for (int k = 0; k < Children.Count; k++)
                    {
                        for (int l = 0; l < Children.Count; l++)
                        {
                            if (l != k)
                            {
                                PHIij = Globals.Parameters.IR_TenorCorr(Children[k], Children[l]);

                                RHOkl = Globals.Parameters.BucketCorr_Rho(this.CalcCcY, Children[k], Children[l]);

                                KSquared += PHIij * RHOkl * Children[k].WeightedScaledSensi * Children[l].WeightedScaledSensi;
                            }
                        }
                        KSquared += Math.Pow(Children[k].WeightedScaledSensi, 2);
                    }
                    Margin = Math.Sqrt(KSquared);
                    //TODO: Not optimum - but ok for now
                    ConcentrationFactor_CRb = Children.Max(x => x.ConcentrationFactor_CRb); // store at bucket level 
                }
                else // if (RiskClass != RiskClassType.IR) ====== NOT IR
                {
                    double KSquared = 0;
                    double Fkl = 0;
                    double RHOkl = 0;
                    for (int k = 0; k < Children.Count; k++)
                    {
                        for (int l = 0; l < Children.Count; l++)
                        {
                            if (l != k)
                            {
                                RHOkl = Globals.Parameters.BucketCorr_Rho(this.CalcCcY, Children[k], Children[l]);

                                Fkl = Math.Min(Children[k].ConcentrationFactor_CRb, Children[l].ConcentrationFactor_CRb) / Math.Max(Children[k].ConcentrationFactor_CRb, Children[l].ConcentrationFactor_CRb);

                                KSquared +=  RHOkl * Fkl * Children[k].WeightedScaledSensi * Children[l].WeightedScaledSensi;
                            }
                        }
                        KSquared += Math.Pow(Children[k].WeightedScaledSensi, 2);
                    }
                    Margin = Math.Sqrt(KSquared);
                }
            }
            MSIMMDeltaIM = Margin;
        }
        void ComputeVegaIM()
        {
            if (Children.Count == 0)
            {
                ChildrenTotalSensi = 0; // needed for INTER-bucket aggregation ????
                ChildrenTotalSensiABS = 0;
                Margin = 0;
            }
            else if (Children.Count == 1)
            {
                ChildrenTotalSensi = Children[0].Margin; // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = Math.Abs(Children[0].Margin);
                Margin = Math.Abs(Children[0].Margin);// squareRoot of square
            }
            else //if (Children.Count > 1)
            {
                ChildrenTotalSensi = Children.Sum(c => c.Margin); // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = Children.Sum(c => Math.Abs(c.Margin));

                double KSquared = 0;
                double F_kl = 0;
                double RHO_kl = 0;
                for (int k = 0; k < Children.Count; k++)
                {
                    for (int l = 0; l < Children.Count; l++)
                    {
                        if (l != k)
                        {

                            F_kl =
                                (RiskClassEnum == SimmRiskClassType.IR) ? 1 :
                                Math.Min(Children[k].ConcentrationFactor_CRb, Children[l].ConcentrationFactor_CRb) / Math.Max(Children[k].ConcentrationFactor_CRb, Children[l].ConcentrationFactor_CRb);

                            RHO_kl = Globals.Parameters.BucketCorrVegaCurv_Rho(this.CalcCcY, Children[k], Children[l]);

                            KSquared += RHO_kl * F_kl * Children[k].WeightedScaledSensi * Children[l].WeightedScaledSensi;
                        }
                    }
                    KSquared += Math.Pow(Children[k].WeightedScaledSensi, 2);
                }
                Margin = Math.Sqrt(KSquared);
            }
            MSIMMVegaIM = Margin;
        }
        void ComputeCurvIM()
        {
            if (Children.Count == 0)
            {
                ChildrenTotalSensi = 0; // needed for INTER-bucket aggregation ????
                ChildrenTotalSensiABS = 0;
                Margin = 0;
            }
            else if (Children.Count == 1)
            {
                ChildrenTotalSensi = Children[0].Margin; // needed for INTER-bucket aggregation  // ---------> CVR_k
                ChildrenTotalSensiABS = Math.Abs(Children[0].Margin);
                Margin = Math.Abs(Children[0].Margin);// squareRoot of square                    // ---------> K_b
            }
            else //if (Children.Count > 1)
            {
                ChildrenTotalSensi = Children.Sum(c => c.Margin); // needed for INTER-bucket aggregation
                ChildrenTotalSensiABS = Children.Sum(c => Math.Abs(c.Margin)); // needed for INTER-bucket aggregation

                double KSquared = 0;
                double RHO_kl = 0;

                for (int k = 0; k < Children.Count; k++)
                {
                    for (int l = 0; l < Children.Count; l++)
                    {
                        if (l != k)
                        {
                            RHO_kl = Globals.Parameters.BucketCorrVegaCurv_Rho(this.CalcCcY, Children[k], Children[l]); ///TODO: CHECK!!! FOR FX
                            KSquared += RHO_kl * RHO_kl * Children[k].Margin * Children[l].Margin;
                        }
                    }
                    KSquared +=  Math.Pow(Children[k].Margin, 2);
                }

                Margin = Math.Sqrt(KSquared);
            }
            MSIMMCurvIM = Margin;
        }
        void ComputeBaseCorrIM()
        {
            if (Children.Count == 0)
            {
                ChildrenTotalSensi = 0; // needed for INTER-bucket aggregation
                Margin = 0;
            }
            else if (Children.Count == 1)
            {
                ChildrenTotalSensi = Children[0].Margin; // needed for INTER-bucket aggregation
                Margin = Math.Abs(Children[0].Margin);// squareRoot of square
            }
            else //if (Children.Count > 1)
            {
                if (RiskClassEnum != SimmRiskClassType.IR)//TODO: NOT NEEDED --- check
                {
                    double KSquared = 0;
                    double RHOkl = 0;
                    for (int k = 0; k < Children.Count; k++)
                    {
                        for (int l = 0; l < Children.Count; l++)
                        {
                            if (l != k)
                            {
                                RHOkl = Globals.Parameters.CRQ_BaseCorr_Rho;

                                KSquared += RHOkl * Children[k].WeightedScaledSensi * Children[l].WeightedScaledSensi;
                            }
                        }
                        KSquared += Math.Pow(Children[k].WeightedScaledSensi, 2);
                    }
                    Margin = Math.Sqrt(KSquared);
                }
            }

            MSIMMBaseCorrIM = Margin;
        }


    }

}



//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm3_IMClass : NodeMargin
    {
        public Simm3_IMClass(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Constructor for: SIMM IMClasses ( SIMM > ProductClass > RiskClass > IMClass )
        /// </summary>
        /// <param name="nodeMarginEnum"></param>
        /// <param name="groupRFs"></param>
        /// <param name="groupCounterpartyIDKey"></param>
        /// <param name="groupJurisdictionEnumKey"></param>
        /// <param name="groupIMModelEnumKey"></param>
        /// <param name="productClassEnumKey"></param>
        /// <param name="riskClassEnumKey"></param>
        /// <param name="iMClassEnumKey"></param>
        public Simm3_IMClass(SimmNodeMarginType nodeMarginEnum,
            List<SimmRiskFactor> groupRFs,
            string groupCounterpartyIDKey,
            SimmJurisdictionType groupJurisdictionEnumKey,
            UmrImModelsType groupIMModelEnumKey,
            UmrProductClassType productClassEnumKey,
            SimmRiskClassType riskClassEnumKey,
            SimmIMClassType iMClassEnumKey) :

            base(nodeMarginEnum, groupRFs, groupCounterpartyIDKey, groupJurisdictionEnumKey, groupIMModelEnumKey, productClassEnumKey, riskClassEnumKey, iMClassEnumKey)

        { }// nothing needed - All in the base constructor

        public Simm3_IMClass(List<SimmRiskFactor> loRiskFactors, string nodeName, SimmRiskClassType riskClassEnum, SimmIMClassType iMComputationEnum)
            : base(loRiskFactors, nodeName, riskClassEnum, iMComputationEnum, SimmNodeMarginType.Bucket)
        {
            GenerateChildren();
        }

        public override void GenerateChildren()
        {
            if (LoRiskFactors.Count > 0)
            {
                var buckets = LoRiskFactors.GroupBy(rf => rf.RealBucket);// do REAL buckets

                foreach (var bucket in buckets)
                {
                    List<SimmRiskFactor> bucketRFs = bucket.ToList();
                    Simm4_Bucket marginElement = new Simm4_Bucket(bucketRFs, bucket.Key, RiskClassEnum, IMClassEnum);
                    Children.Add(marginElement);
                }
            }

            LoRiskFactors = null;
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

                case SimmIMClassType.None: // catching error
                default:
                    Margin = -1111;
                    MSIMMDeltaIM = Margin;
                    MSIMMVegaIM = Margin;
                    MSIMMCurvIM = Margin;
                    MSIMMBaseCorrIM = Margin;
                    break;
            }

            //// we need to reduce after computation
            //ReduceChildren();
        }

        public void ComputeDeltaIM()
        {
            double imMarginSquared = 0;
            double Gammabc = 0;
            double Sb = 0;
            double Sc = 0;
            if (Children.Count == 0)
            { 
                Margin = 0;
            }
            else if (Children.Count == 1)
            { 
                Margin = Math.Abs(Children[0].Margin);
            }
            else //if (Children.Count > 1)
            {
                if (RiskClassEnum != SimmRiskClassType.IR)
                {
                    double Kresidual = 0;
                    for (int b = 0; b < Children.Count; b++)
                    {
                        if (Children[b].NodeName == "Residual")
                        {
                            Kresidual = Children[b].Margin;// store for the end
                        }
                        else //if (Children[b].NodeName != "Residual")
                        {
                            for (int c = 0; c < Children.Count; c++)
                            {
                                if ((c != b) && (Children[c].NodeName != "Residual"))
                                {
                                    Gammabc = Globals.Parameters.BucketCorr_Gamma(Children[b], Children[c]);

                                    Sb = Math.Max(Math.Min(Children[b].ChildrenTotalSensi, Children[b].Margin), -Children[b].Margin);
                                    Sc = Math.Max(Math.Min(Children[c].ChildrenTotalSensi, Children[c].Margin), -Children[c].Margin);

                                    imMarginSquared +=  Gammabc * Sb * Sc;
                                }
                            }
                            imMarginSquared += Math.Pow(Children[b].Margin, 2);
                        }
                    }
                    Margin = Math.Sqrt(imMarginSquared) + Kresidual;
                }
                else// (RiskClass == RiskClassType.IR)
                {
                    double GGbc = 0;
                    double CRb = 0;
                    double CRc = 0;

                    for (int b = 0; b < Children.Count; b++)
                    {
                        // TODO: better if it were done @ margin level (we already have the TotalSensi)
                        //       compute CRb at bucket level, then use it here too
                        CRb = Children[b].Children.Max(x => x.ConcentrationFactor_CRb);  // all should be same except for XCcyBasis

                        for (int c = 0; c < Children.Count; c++)
                        {
                            if (c != b)
                            {
                                CRc = Children[c].Children.Max(x => x.ConcentrationFactor_CRb);  // all should be same except for XCcyBasis
                                Gammabc = Globals.Parameters.IR_OuterCorr_Gamma;

                                GGbc = (Math.Max(CRb, CRc)==0)? 1: Math.Min(CRb, CRc) / Math.Max(CRb, CRc);
                                
                                Sb = Math.Max(Math.Min(Children[b].ChildrenTotalSensi, Children[b].Margin), -Children[b].Margin);
                                Sc = Math.Max(Math.Min(Children[c].ChildrenTotalSensi, Children[c].Margin), -Children[c].Margin);

                                imMarginSquared +=  Gammabc * GGbc * Sb * Sc;
                            }
                        }
                        imMarginSquared += Math.Pow(Children[b].Margin, 2);
                    }
                    Margin = Math.Sqrt(imMarginSquared);
                }
            }

            MSIMMDeltaIM = Margin;// Storing in DeltaIM as well
        }

        public void ComputeVegaIM()
        {
            if (Children.Count == 0) Margin = 0;
            else if (Children.Count == 1) Margin = Math.Abs(Children[0].Margin);
            else //if (Children.Count > 1)
            {
                double GGbc = 0;
                double Gammabc = 0;
                double CRb = 0;
                double CRc = 0;
                double Sb = 0;
                double Sc = 0;
                double imMarginSquared = 0;
                double Kresidual = 0;

                for (int b = 0; b < Children.Count; b++)
                {
                    if (Children[b].NodeName == "Residual")
                    {
                        Kresidual = Children[b].Margin;// store for the end
                    }
                    else if (Children[b].NodeName != "Residual")
                    {
                        CRb = Children[b].Children.Max(x => x.ConcentrationFactor_CRb);  // all should be same except for XCcyBasis

                        for (int c = 0; c < Children.Count; c++)
                        {
                            if ((c != b) && (Children[c].NodeName != "Residual"))
                            {
                                CRc = Children[c].Children.Max(x => x.ConcentrationFactor_CRb);  // all should be same except for XCcyBasis

                                Gammabc = Globals.Parameters.BucketCorr_Gamma(Children[b], Children[c]);

                                GGbc = (RiskClassEnum == SimmRiskClassType.IR) ? Math.Min(CRb, CRc) / Math.Max(CRb, CRc) : 1; // 1 for all classes except IR
                                
                                Sb = Math.Max(Math.Min(Children[b].ChildrenTotalSensi, Children[b].Margin), -Children[b].Margin);
                                Sc = Math.Max(Math.Min(Children[c].ChildrenTotalSensi, Children[c].Margin), -Children[c].Margin);
                                imMarginSquared += Gammabc * GGbc * Sb * Sc;
                            }

                        }
                        imMarginSquared += Math.Pow(Children[b].Margin, 2);
                    }
                }

                Margin = Math.Sqrt(imMarginSquared) + Kresidual;
            }
            
            MSIMMVegaIM = Margin;// Storing in VegaIM as well
        }

        public void ComputeCurvIM()
        {
            if (Children.Count == 0) Margin = 0;
            //else if (Children.Count == 1) Margin = Math.Abs(Children[0].Margin);
            else //if (Children.Count > 0) /// ######## EVEN IF COUNT = 1  #################
            {
                bool nonResidualPRESENT = false;
                double theta = 0;// ChildrenTotalSensi = Children.Sum(c => c.Margin); // needed for INTER-bucket aggregation
                double lambda = 0;
                double sum = 0;
                double sumAbs = 0;

                bool residualPRESENT = false;
                double thetaResidual = 0;
                double lambdaResidual = 0;
                double sumResidual = 0;
                double sumResidualAbs = 0;
                double Kresidual = 0;

                foreach (Simm4_Bucket bucketME in Children)
                {
                    if (bucketME.NodeName == "Residual")
                    {
                        residualPRESENT = true;

                        sumResidual += bucketME.ChildrenTotalSensi;

                        sumResidualAbs += bucketME.ChildrenTotalSensiABS;

                        Kresidual = bucketME.Margin;
                    }
                    else //if (bucketME.NodeName != "Residual")
                    {
                        nonResidualPRESENT = true;

                        sum += bucketME.ChildrenTotalSensi;

                        sumAbs += bucketME.ChildrenTotalSensiABS;

                        bucketME.S_b = Math.Max(Math.Min(bucketME.ChildrenTotalSensi, bucketME.Margin), -bucketME.Margin);
                    }
                }

                // NON - Residual // -------------------------------------------------------------------------------------------------
                double curvMarginNONresidual = 0;
                double curvMarginNONresiTEMP = 0;
                double totalChildrenTotalSensi = 0;
                double gamma = 0;
                if ((nonResidualPRESENT == true) && (sumAbs > 0))
                {
                    theta = Math.Min(sum / sumAbs, 0);
                    lambda = (Math.Pow(MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, .995), 2) - 1) * (1 + theta) - theta;

                    for (int k = 0; k < Children.Count; k++)
                    {
                        if (Children[k].NodeName != "Residual")
                        {
                            for (int l = 0; l < Children.Count; l++)
                            {
                                if ((l != k) && (Children[l].NodeName != "Residual"))
                                {
                                    gamma = Globals.Parameters.BucketCorr_Gamma(Children[k], Children[l]);
                                    curvMarginNONresiTEMP +=  gamma * gamma * Children[k].S_b * Children[l].S_b;
                                }
                            }

                            totalChildrenTotalSensi += Children[k].ChildrenTotalSensi;
                            curvMarginNONresiTEMP +=  Children[k].Margin * Children[k].Margin;// K ^ 2
                        }
                    }
                    curvMarginNONresiTEMP = Math.Sqrt(curvMarginNONresiTEMP);
                    curvMarginNONresiTEMP = lambda * curvMarginNONresiTEMP;
                    curvMarginNONresiTEMP +=  totalChildrenTotalSensi;

                    curvMarginNONresidual = Math.Max(curvMarginNONresiTEMP, 0);

                    // ---- SPECIAL CASE --- INTEREST RATES --------------------//
                    if (RiskClassEnum == SimmRiskClassType.IR)
                        curvMarginNONresidual /= Math.Pow(Globals.Parameters.HVR_IR, 2);
                }

                // Residual // -------------------------------------------------------------------------------------------------------
                double curvMarginResidual = 0;
                if ((residualPRESENT == true) && (sumResidualAbs > 0))
                {
                    thetaResidual = Math.Min(sumResidual / sumResidualAbs, 0);
                    lambdaResidual = (Math.Pow(MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, .995), 2) - 1) * (1 + thetaResidual) - thetaResidual;

                    curvMarginResidual = Math.Max(sumResidual + lambdaResidual * Kresidual, 0);
                }

                // TOTAL CURV  // -------------------------------------------------------------------------------------------------------
                Margin = curvMarginNONresidual + curvMarginResidual;
            }
            
            MSIMMCurvIM = Margin;// Storing in CurvIM as well
        }

        public void ComputeBaseCorrIM()
        {
            if (Children.Count == 0) Margin = 0;
            else if (Children.Count == 1) Margin = Math.Abs(Children[0].Margin);
            else
                throw new Exception("Error in Tree: BaseCorr should only have one bucket");

            MSIMMBaseCorrIM = Margin;// Storing in BaseCorrIM as well
        }

        
    }

}

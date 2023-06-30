//using OM.Common.Enums;
//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    //class ME6_SubBucket
    //{
    //}
    public class Simm5_SubBucket : NodeMargin
    {
        public Simm5_SubBucket(List<SimmRiskFactor> loRiskFactors, string nodeName, SimmRiskClassType riskClass, SimmIMClassType iMComputationType, double sensiTotalforConcentration, string parentRealBucketName)
            : base(loRiskFactors, nodeName, riskClass, iMComputationType, sensiTotalforConcentration, parentRealBucketName, SimmNodeMarginType.SubBucket)
        {
            //this.NodeLevelName = "SubBucket"; // NO LONGER IN USE

            GenerateChildren();// not doing anything as it is not overriden
        }


        //// not implemented as there are no children
        //public override void GenerateChildren()
        //{
        //    base.GenerateChildren();
        //}

        public override void NodeCompute()
        {
            //// Compute Children
            //foreach (var child in Children)
            //    child.NodeCompute();

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

            //LoRiskFactors = null; // can't do that yet
        }

        private void ComputeDeltaIM()
        {
            if (LoRiskFactors.Count > 0)
            {
                if ((RiskClassEnum == SimmRiskClassType.CR) || (RiskClassEnum == SimmRiskClassType.CRNQ))
                {
                    SimmRiskFactor rf = LoRiskFactors[0];// ======== WE USE THE FIRST ONE FOR INFO === but could be more than 1 - i.e. diff ccies, or maturities (a.k.a. vertex)

                    RiskWeight = Globals.Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);// Get RW - Risk Weight for each Risk Factor (k)

                    ConcentrationFactor_CRb = Globals.Parameters.DeltaConcentrationRiskFactor(Globals.Settings.CalcCcy, rf, SensiTotalforConcentration);// Get CR - Concentration Risk Factor for each Risk Factor (k)

                    WeightedScaledSensi = RiskWeight * rf.AmountUSD * ConcentrationFactor_CRb;

                    Margin = WeightedScaledSensi;
                }
                else if ((RiskClassEnum == SimmRiskClassType.EQ) || (RiskClassEnum == SimmRiskClassType.COM) || (RiskClassEnum == SimmRiskClassType.FX))
                {

                    SimmRiskFactor rf = LoRiskFactors[0];// ======== CHECK ====== ONLY 1 RF if EQ, COM, FX

                    RiskWeight = Globals.Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);// Get RW - Risk Weight for each Risk Factor (k)

                    ConcentrationFactor_CRb = Globals.Parameters.DeltaConcentrationRiskFactor(Globals.Settings.CalcCcy, rf, LoRiskFactors);// Get CR - Concentration Risk Factor for each Risk Factor (k)

                    WeightedScaledSensi = RiskWeight * rf.AmountUSD * ConcentrationFactor_CRb;

                    Margin = WeightedScaledSensi;

                }
                else// (RiskClass == RiskClassType.IR)
                {
                    SimmRiskFactor rf = LoRiskFactors[0];// ======== WE USE THE FIRST ONE FOR INFO === but could be more than 1 - i.e. diff ccies, or maturities (a.k.a. vertex)

                    RiskWeight = Globals.Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);// Get RW - Risk Weight for each Risk Factor (k)

                    ConcentrationFactor_CRb = Globals.Parameters.DeltaConcentrationRiskFactor(Globals.Settings.CalcCcy, rf, SensiTotalforConcentration);// Get CR - Concentration Risk Factor for each Risk Factor (k)

                    WeightedScaledSensi = RiskWeight * rf.AmountUSD * ConcentrationFactor_CRb;

                    Margin = WeightedScaledSensi;
                }
            }
        }
        private void ComputeVegaIM()
        {
            //double rw;

            double sigma;
            double HVR;
            //double VegaRiskWeight;// = Parameters.GetVegaRiskWeight(Globals.Settings.CalcCcy, LoRiskFactors[0]); 
            //double VegaConcRiskFactor_b;
            double VegaRISK_k;

            double SumVEGARISKS_ik = 0;

            if (LoRiskFactors.Count > 0)
            {

                //TODO:  ############ IF THE FOR EACHE USED / OBSOLETE ????? ###################
                foreach (SimmRiskFactor rf in LoRiskFactors)// int riskId = 0;// { ir, Cred, CredNOq, Eq, Comm, Fx }
                {
                    if ((RiskClassEnum == SimmRiskClassType.IR) || (RiskClassEnum == SimmRiskClassType.CR) || (RiskClassEnum == SimmRiskClassType.CRNQ)) // Risk Class = 1, 2
                    {
                        SumVEGARISKS_ik += rf.AmountUSD;
                    }
                    else // 3, 4, 5
                    {
                        //rw = Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);// Get RW - Risk Weight for each Risk Factor (k)

                        RiskWeight = Globals.Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);// Get RW - Risk Weight for each Risk Factor (k) // = rw;

                        sigma = RiskWeight * Math.Sqrt(365.0 / (1.4 * Globals.Parameters.mpor)) / MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, .99);

                        HVR =
                            (RiskClassEnum == SimmRiskClassType.EQ) ? Globals.Parameters.HVR_Equity :
                            (RiskClassEnum == SimmRiskClassType.COM) ? Globals.Parameters.HVR_Commodity :
                            Globals.Parameters.HVR_FX;

                        SumVEGARISKS_ik += rf.AmountUSD * sigma * HVR;
                    }

                }

                //return SumVEGARISKSik;
                if (SensiTotalforConcentration == 0)// NOT IR or CR or CRNV
                    SensiTotalforConcentration = SumVEGARISKS_ik;

                RiskWeight = Globals.Parameters.GetVegaRiskWeight(Globals.Settings.CalcCcy, LoRiskFactors[0]); //= VegaRiskWeight;

                //// 2- compute the VegaConcentration Risk Factor (denominator needs computing)
                ConcentrationFactor_CRb = Globals.Parameters.VegaConcentrationRiskFactor(Globals.Settings.CalcCcy, LoRiskFactors[0], SensiTotalforConcentration);// 0- get the SUM of VegaRisks == BUCKET LEVEL === across subCurve
                //SubBucketME.ConcentrationFactor_CRb = VegaConcRiskFactor_b;

                VegaRISK_k = RiskWeight * SumVEGARISKS_ik * ConcentrationFactor_CRb;

                WeightedScaledSensi = VegaRISK_k;
                Margin = VegaRISK_k;
            }
        }
        private void ComputeCurvIM()
        {
            double rw;
            double sigma_ik;

            double CurvRiskSum = 0;
            double CurvRisk_ik;
            double dayUnit;
            double days;

            double scale;  // Scale from Scaling Function
            if (LoRiskFactors.Count > 0)
            {
                foreach (SimmRiskFactor rf in LoRiskFactors)// int riskId = 0;// { ir, Cred, CredNOq, Eq, Comm, Fx }
                {
                    // Special for EQ volatility indices
                    if ((rf.Enum4RiskClass == SimmRiskClassType.EQ) && (rf.Bucket == "12"))
                    {
                        CurvRisk_ik = 0;
                        continue;
                    }


                    if ((RiskClassEnum == SimmRiskClassType.IR) || (RiskClassEnum == SimmRiskClassType.CR) || (RiskClassEnum == SimmRiskClassType.CRNQ))
                    {
                        sigma_ik = 1;
                    }
                    else
                    {
                        rw = Globals.Parameters.GetDeltaRiskWeight(Globals.Settings.CalcCcy, rf);
                        sigma_ik = rw * Math.Sqrt(365.0 / (1.4 * Globals.Parameters.mpor)) / MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, .99);
                    }
                    
                    char timeScale = rf.Label1.Last();
                    dayUnit =
                        timeScale == 'y' ? 365 :
                        timeScale == 'm' ? 365.0 / 12 :
                        7;
                    days = dayUnit * Double.Parse(rf.Label1.Substring(0, rf.Label1.Length - 1));
                    scale = 0.5 * Math.Min(1, 14 / days) * ((double)Globals.Parameters.mpor / 10.0);
                    
                    CurvRisk_ik = scale * sigma_ik * rf.AmountUSD;
                    CurvRiskSum += CurvRisk_ik;
                }
            }
            
            Margin = CurvRiskSum;
        }
        private void ComputeBaseCorrIM()
        {
            if (LoRiskFactors.Count > 0)
            {
                SimmRiskFactor rf = LoRiskFactors[0];

                RiskWeight = Globals.Parameters.CRQ_DeltaBaseCorrRiskWeight;//// Get BaseCorr Risk Weight for each Risk Factor (k) ------- same for all

                WeightedScaledSensi = RiskWeight * rf.AmountUSD;

                Margin = WeightedScaledSensi; // NO COMPUTE AT THIS LEVEL
            }

        }
    }



}


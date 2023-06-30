//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm1_AddOnMultiplier : NodeMargin
    {
        // 4 ProductClass Multipliers initial values
        private double RatesFX_Multi = 1;
        private double Credit_Multi = 1;
        private double Equity_Multi = 1;
        private double Commodity_Multi = 1;

        public Simm1_AddOnMultiplier(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Constructor for: SIMM AddOns ( SIMM > AddOns )
        /// </summary>
        /// <param name="nodeMarginEnum"></param>
        /// <param name="groupRFs"></param>
        /// <param name="groupCounterpartyIDKey"></param>
        /// <param name="groupJurisdictionEnumKey"></param>
        /// <param name="groupIMModelEnumKey"></param>
        public Simm1_AddOnMultiplier(SimmNodeMarginType nodeMarginEnum,
            List<SimmRiskFactor> groupRFs,
            string groupCounterpartyIDKey,
            SimmJurisdictionType groupJurisdictionEnumKey,
            UmrImModelsType groupIMModelEnumKey) :

            base(nodeMarginEnum, groupRFs, groupCounterpartyIDKey, groupJurisdictionEnumKey, groupIMModelEnumKey)

        { }// nothing needed - All in the base constructor

        public Simm1_AddOnMultiplier(List<SimmRiskFactor> loRiskFactors) : base(loRiskFactors, "SIMMAddOnMultiplier", SimmNodeMarginType.SIMMAddOnMultiplier)
        {
            //GenerateChildren();// nothing to do for this "Addon" - we simply don't override empty method
        }

        //public override void NodeCompute() // not overriding on purpose. Multiplier node needs to be computed by Parent (after Simm ProductClasses have all been computed)
        //{ 
        //}

        public void SetMultiplier()
        {
            if (LoRiskFactors != null )
            {
                foreach (SimmRiskFactor rf in LoRiskFactors)
                {
                    switch (rf.Qualifier)
                    {
                        case "RatesFX":
                            RatesFX_Multi = rf.AmountUSD;
                            break;

                        case "Credit":
                            Credit_Multi = rf.AmountUSD;
                            break;

                        case "Equity":
                            Equity_Multi = rf.AmountUSD;
                            break;

                        case "Commodity":
                            Commodity_Multi = rf.AmountUSD;
                            break;
                    }
                }
            }
        }

        public void Compute_SIMM_MultiplierAddon_Margin(double simmRatesFX, double simmCredit, double simmEquity, double simmCommodity)
        {
            MSIMM_AddOn_Multiplier = simmRatesFX * (RatesFX_Multi - 1) + simmCredit * (Credit_Multi - 1) + simmEquity * (Equity_Multi - 1) + simmCommodity * (Commodity_Multi - 1);
            Margin = MSIMM_AddOn_Multiplier;
        }


    }

}

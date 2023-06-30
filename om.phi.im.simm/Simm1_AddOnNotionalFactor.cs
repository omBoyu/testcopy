//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm1_AddOnNotionalFactor : NodeMargin
    {
        public Simm1_AddOnNotionalFactor(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Constructor for: SIMM AddOns ( SIMM > AddOns )
        /// </summary>
        /// <param name="nodeMarginEnum"></param>
        /// <param name="groupRFs"></param>
        /// <param name="groupCounterpartyIDKey"></param>
        /// <param name="groupJurisdictionEnumKey"></param>
        /// <param name="groupIMModelEnumKey"></param>
        public Simm1_AddOnNotionalFactor(SimmNodeMarginType nodeMarginEnum,
            List<SimmRiskFactor> groupRFs,
            string groupCounterpartyIDKey,
            SimmJurisdictionType groupJurisdictionEnumKey,
            UmrImModelsType groupIMModelEnumKey) :

            base(nodeMarginEnum, groupRFs, groupCounterpartyIDKey, groupJurisdictionEnumKey, groupIMModelEnumKey)

        { }// nothing needed - All in the base constructor

        public Simm1_AddOnNotionalFactor(List<SimmRiskFactor> loRiskFactors) : base(loRiskFactors, SimmNodeMarginType.AddOnNotionalFactor.ToString(), SimmNodeMarginType.AddOnNotionalFactor)
        {
            GenerateChildren();// nothing to do for "Addon" - we simply don't override empty method
        }

        public override void NodeCompute()
        {
            double addonNotionalFactor = 0;

            List<SimmRiskFactor> addonFactorsRFs = LoRiskFactors.Where(x => x.RiskTypeEnum == SimmRiskTypeType.Param_AddOnNotionalFactor).ToList();

            if (addonFactorsRFs.Count > 0)
            {
                foreach (SimmRiskFactor factorRF in addonFactorsRFs)
                {
                    double totalNotionalForFactor = LoRiskFactors.Where(x => (x.RiskTypeEnum == SimmRiskTypeType.Notional) && (x.Qualifier == factorRF.Qualifier)).Sum(rf => rf.AmountUSD);

                    addonNotionalFactor += totalNotionalForFactor * factorRF.AmountUSD / 100;
                }
            }

            MSIMM_AddOn_NotionalFactor = addonNotionalFactor;
            Margin = addonNotionalFactor;
        }

    }

}

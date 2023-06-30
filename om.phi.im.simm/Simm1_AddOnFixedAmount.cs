//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm1_AddOnFixedAmount : NodeMargin
    {
        public Simm1_AddOnFixedAmount(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Constructor for: SIMM AddOns ( SIMM > AddOns )
        /// </summary>
        /// <param name="nodeMarginEnum"></param>
        /// <param name="groupRFs"></param>
        /// <param name="groupCounterpartyIDKey"></param>
        /// <param name="groupJurisdictionEnumKey"></param>
        /// <param name="groupIMModelEnumKey"></param>
        public Simm1_AddOnFixedAmount(SimmNodeMarginType nodeMarginEnum,
            List<SimmRiskFactor> groupRFs,
            string groupCounterpartyIDKey,
            SimmJurisdictionType groupJurisdictionEnumKey,
            UmrImModelsType groupIMModelEnumKey):

            base(nodeMarginEnum, groupRFs, groupCounterpartyIDKey, groupJurisdictionEnumKey, groupIMModelEnumKey)

        { }// nothing needed - All in the base constructor

        public Simm1_AddOnFixedAmount(List<SimmRiskFactor> loRiskFactors) : base(loRiskFactors, SimmNodeMarginType.AddOnFixedAmount.ToString(), SimmNodeMarginType.AddOnFixedAmount)
        {
            GenerateChildren();// nothing to do for "Addon" - we simply don't override empty method
        }

        public override void NodeCompute()
        {
            MSIMM_AddOn_FixedAmount = LoRiskFactors.Sum(rf => rf.AmountUSD);
            Margin = MSIMM_AddOn_FixedAmount;
        }

    }

}

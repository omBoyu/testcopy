//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;

namespace om.phi.im.simm
{
    public class Simm0 : NodeMargin
    {
        private List<SimmRiskFactor> loRiskFactorsSaved { get; set; }

        public Simm0(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Reconstructing the tree, just creating node with (already) computed children
        /// </summary>
        /// <param name="loMarginNodes"></param>
        public Simm0(List<NodeMargin> loMarginNodes) : base(SimmNodeMarginType.Simm, loMarginNodes)
        {
            // all done in base constructor
        }

        public Simm0(List<SimmRiskFactor> loRiskFactors, bool computationIsByTree) : base(loRiskFactors, SimmNodeMarginType.Simm.ToString(), SimmNodeMarginType.Simm)
        {
            IMModelsEnum = UmrImModelsType.SIMM;

            Consolidate_SIMM_SIMM_RiskFactors_For_TREE_VIEW();// IMPORTANT SO THAT WE HAVE UNIQUENESS

            //if (loRiskFactors.Count > 0) // no need --- we only get there if Count>0
            GenerateChildren();
        }
        // comment test
        //private void Save_RiskFactors_For_TRADE_VIEW()
        //{
        //    loRiskFactorsSaved = new List<SimmRiskFactor>();
        //    foreach (var rf in LoRiskFactors)
        //    {
        //        loRiskFactorsSaved.Add(new RiskFactor(rf)); // Clone Constructor
        //    }
        //}
        private void Consolidate_SIMM_SIMM_RiskFactors_For_TREE_VIEW()
        {
            var consolidatedRiskFactors =
                        from x in LoRiskFactors
                        group x by new
                        {
                            x.Enum3ProductClass,
                            x.RiskTypeEnum,
                            x.Enum4RiskClass,
                            x.Enum5IMClass,

                            x.Qualifier,
                            x.Bucket,
                            x.Label1,
                            x.Label2,
                            x.AmountCurrency,

                            x.RealBucket,
                            x.SubBucket,
                            x.SubBucketCurv
                        }
                        into y
                        select new SimmRiskFactor()
                        {
                            Enum3ProductClass = y.Key.Enum3ProductClass,
                            RiskTypeEnum = y.Key.RiskTypeEnum,
                            Enum4RiskClass = y.Key.Enum4RiskClass,
                            Enum5IMClass = y.Key.Enum5IMClass,

                            Qualifier = y.Key.Qualifier,
                            Bucket = y.Key.Bucket,
                            Label1 = y.Key.Label1,
                            Label2 = y.Key.Label2,
                            AmountCurrency = y.Key.AmountCurrency,

                            RealBucket = y.Key.RealBucket,
                            SubBucket = y.Key.SubBucket,
                            SubBucketCurv = y.Key.SubBucketCurv,

                            Amount = y.Sum(rf => rf.Amount),
                            AmountUSD = y.Sum(rf => rf.AmountUSD),
                        };

            LoRiskFactors = consolidatedRiskFactors.ToList();

        }

        public override void GenerateChildren()
        {
            // First, the Addons - they have no ProductClass ----------------------------------------------------------------------------------
            //List<SimmRiskFactor> selectedRFs;
            bool paramProductClassMuliplierGenerated = false;


            var groupedRFs = LoRiskFactors.GroupBy(rf => rf.Enum3ProductClass);
            foreach (var groupPC in groupedRFs)
            {

                if (groupPC.Key != UmrProductClassType.None) // it is a real ProductClass= RatesFX, Credit, Equities, Commodity
                {
                    Children.Add(new Simm1_ProductClass(groupPC.ToList(), groupPC.Key));
                }
                else // it's for Addon => UmrProductClassType.None
                {
                    // for AddonFactor --- we need to UNION 2 groups --> so we will collect the RFs of the 2 groups
                    List<SimmRiskFactor> addOnFactorALLriskFactors = new List<SimmRiskFactor>();

                    var addonsRFsGroups = groupPC.ToList().GroupBy(rf => rf.RiskTypeEnum);
                    foreach (var group in addonsRFsGroups)
                    {
                        if (group.Key == SimmRiskTypeType.Param_ProductClassMultiplier)
                        {
                            Children.Add(new Simm1_AddOnMultiplier(group.ToList()));
                            paramProductClassMuliplierGenerated = true;
                        }
                        else if (group.Key == SimmRiskTypeType.Param_AddOnFixedAmount)
                            Children.Add(new Simm1_AddOnFixedAmount(group.ToList()));

                        else // SimmRiskTypeType.Param_AddOnNotionalFactor || SimmRiskTypeType.Notional
                            addOnFactorALLriskFactors.AddRange(group.ToList()); // we go through this for RiskTypeEnum = Notionan AND RiskTypeEnum = Param_AddOnNotionalFactor
                    }

                    if (addOnFactorALLriskFactors.Count>0) // now we can create the AddOnNotionalFactor node (with all the RFs)
                        Children.Add(new Simm1_AddOnNotionalFactor(addOnFactorALLriskFactors));
                }

            }
            // ensure SIMMAddOnMultiplier is always present
            if (!paramProductClassMuliplierGenerated)
                Children.Add(new Simm1_AddOnMultiplier(new List<SimmRiskFactor>()));// just pass an EMPTY list of RFs


            LoRiskFactors = null;
        }

        public override void NodeCompute()
        {
            // Compute Children
            foreach (var child in Children)
                child.NodeCompute();

            NodeComputeNodeOnly();
        }
        public override void NodeComputeNodeOnly()
        {
            // All children except AddOn_Multiplier have been computed
            double simmRatesFX = 0;
            double simmCredit = 0;
            double simmEquity = 0;
            double simmCommodity = 0;

            foreach (var productClass in Children)
            {
                switch (productClass.ProductClassEnum)
                {
                    case UmrProductClassType.RatesFX:
                        simmRatesFX = productClass.Margin;
                        break;

                    case UmrProductClassType.Credit:
                        simmCredit = productClass.Margin;
                        break;

                    case UmrProductClassType.Equity:
                        simmEquity = productClass.Margin;
                        break;

                    case UmrProductClassType.Commodity:
                        simmCommodity = productClass.Margin;
                        break;

                    case UmrProductClassType.None:// for all the AddOns (incl. multiplier)
                        break;

                    case UmrProductClassType.Rates:// catching error - Schedule ------------
                    case UmrProductClassType.FX:// catching error - Schedule ---------------
                    case UmrProductClassType.Other:// catching error - Schedule ------------
                    default:
                        simmRatesFX = -3333;
                        simmCredit = -3333;
                        simmEquity = -3333;
                        simmCommodity = -3333;
                        break;
                }
                //if (productClass.NodeName == UmrProductClassType.RatesFX.ToString()) simmRatesFX = productClass.Margin;
                //if (productClass.NodeName == UmrProductClassType.Credit.ToString()) simmCredit = productClass.Margin;
                //if (productClass.NodeName == UmrProductClassType.Equity.ToString()) simmEquity = productClass.Margin;
                //if (productClass.NodeName == UmrProductClassType.Commodity.ToString()) simmCommodity = productClass.Margin;
            }

            // the Child = AddOn_Multiplier needs to be computed NOW --- could not do before bc Product Class node had not been computed
            // debug --------------------------------------------------------------------------------------------------------------------------
            List<NodeMargin> nodesXXX = Children.Where(x => x.NodeMarginEnum == SimmNodeMarginType.SIMMAddOnMultiplier).ToList();   
            int nodesXXXCount = nodesXXX.Count;
            // debug --------------------------------------------------------------------------------------------------------------------------
            // get node
            Simm1_AddOnMultiplier nodeX = (Simm1_AddOnMultiplier)Children.Where(x => x.NodeMarginEnum == SimmNodeMarginType.SIMMAddOnMultiplier).First();
            nodeX.SetMultiplier();
            nodeX.Compute_SIMM_MultiplierAddon_Margin(simmRatesFX, simmCredit, simmEquity, simmCommodity);

            // now we can reduce them all before computation
            ReduceChildren();

            MSIMMDeltaIM = Children.Sum(child => child.MSIMMDeltaIM);// 0 for Addon child
            MSIMMVegaIM = Children.Sum(child => child.MSIMMVegaIM);// 0 for Addon child
            MSIMMCurvIM = Children.Sum(child => child.MSIMMCurvIM);// 0 for Addon child
            MSIMMBaseCorrIM = Children.Sum(child => child.MSIMMBaseCorrIM);// 0 for Addon child

            MSIMM_AddOn_Multiplier = Children.Sum(child => child.MSIMM_AddOn_Multiplier);// 0 for the others
            MSIMM_AddOn_FixedAmount = Children.Sum(child => child.MSIMM_AddOn_FixedAmount);// 0 for the others
            MSIMM_AddOn_NotionalFactor = Children.Sum(child => child.MSIMM_AddOn_NotionalFactor);// 0 for the others


            // all done, we can compute final
            MSIMMAddOnIM_ISDA = MSIMM_AddOn_Multiplier + MSIMM_AddOn_FixedAmount + MSIMM_AddOn_NotionalFactor;

            MSIMM = Children.Sum(child => child.Margin);
            Margin = MSIMM;
        }
    }

}

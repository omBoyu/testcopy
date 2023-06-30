//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm2_RiskClass : NodeMargin
    {
        public SimmRiskClassType riskClass;


        public Simm2_RiskClass(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Reconstructing the tree, just creating node with (already) computed children
        /// </summary>
        /// <param name="loMarginNodes"></param>
        public Simm2_RiskClass(List<NodeMargin> loMarginNodes) : base(SimmNodeMarginType.RiskClass, loMarginNodes)
        {
            // all done in base constructor
        }

        public Simm2_RiskClass(List<SimmRiskFactor> loRiskFactors, SimmRiskClassType riskClassEnum) : base(loRiskFactors, riskClassEnum, SimmNodeMarginType.RiskClass)
        {
            // check is needed ---- could be empty
            if (loRiskFactors.Count>0) // we need to have all 6 RiskClasses (even if 0), for the correlated aggregation, but no need to go below
                GenerateChildren();
            
        }



        public override void GenerateChildren()
        {
            var iMClassRiskFactorsGroups = LoRiskFactors.GroupBy(rf => rf.Enum5IMClass);
            foreach (var group in iMClassRiskFactorsGroups)
            {
                if (group.Key != SimmIMClassType.None)// we never get a group for CurvIM
                {
                    Children.Add(new Simm3_IMClass(group.ToList(), group.Key.ToString(), RiskClassEnum, group.Key));

                    if (group.Key == SimmIMClassType.VegaIM)// special treatment // RFs for CurvIM are same as for VegaIM
                        Children.Add(new Simm3_IMClass(group.ToList(), "CurvIM", RiskClassEnum, SimmIMClassType.CurvIM));
                }
            }
            
            LoRiskFactors = null;// no more need
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
            Margin = Children.Sum(child => child.Margin);

            // store the IMs as well
            foreach (var child in Children)
            {
                switch (child.IMClassEnum)
                {
                    case SimmIMClassType.DeltaIM:
                        MSIMMDeltaIM = child.Margin; 
                        break;
                    case SimmIMClassType.VegaIM:
                        MSIMMVegaIM = child.Margin; 
                        break;
                    case SimmIMClassType.CurvIM:
                        MSIMMCurvIM = child.Margin; 
                        break;
                    case SimmIMClassType.BaseCorrIM:
                        MSIMMBaseCorrIM = child.Margin; 
                        break;

                    case SimmIMClassType.None:// catching error
                    default:
                        Margin = -2222;
                        MSIMMDeltaIM = Margin;
                        MSIMMVegaIM = Margin;
                        MSIMMCurvIM = Margin;
                        MSIMMBaseCorrIM = Margin;
                        break;
                }
            }

            // all computed, we can reduce below
            ReduceDescendants();

        }
    }

}

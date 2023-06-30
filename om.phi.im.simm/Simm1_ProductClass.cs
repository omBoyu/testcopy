using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
//using OM.Common.Enums;
//using OM.Common.Umr;
using OM.Classes;
using OM.Enums;
using om.phi.im.umr;


namespace om.phi.im.simm
{
    public class Simm1_ProductClass : NodeMargin
    {
        //public ProductClassEnum productClass;


        public Simm1_ProductClass(NodeMargin marginNode, bool isForCastingDownstream) : base(marginNode, isForCastingDownstream) { }
        /// <summary>
        /// Reconstructing the tree, just creating node with (already) computed children
        /// </summary>
        /// <param name="loMarginNodes"></param>
        public Simm1_ProductClass(List<NodeMargin> loMarginNodes) : base(SimmNodeMarginType.SIMMProductClass, loMarginNodes)
        {
            // all done in base constructor
        }

        /// <summary>
        /// DO NOT USE ---- IT IS NOT SETTING THE   UmrProductClassType
        /// </summary>
        /// <param name="loRiskFactors"></param>
        /// <param name="nodeName"></param>
        public Simm1_ProductClass(List<SimmRiskFactor> loRiskFactors, string nodeName) : base(loRiskFactors, nodeName, SimmNodeMarginType.SIMMProductClass)
        {
            // no check needed - done upstream
            //if (loRiskFactors.Count > 0) // we need to have all 6 RiskClasses (even if 1), for the correlated aggregation, but 0 --- No Need
            GenerateChildren();
        }
        public Simm1_ProductClass(List<SimmRiskFactor> loRiskFactors, UmrProductClassType productClassEnum) : base(loRiskFactors, productClassEnum.ToString(), SimmNodeMarginType.SIMMProductClass)
        {
            ProductClassEnum = productClassEnum;
            
            // no check needed - done upstream
            //if (loRiskFactors.Count > 0) // we need to have all 6 RiskClasses (even if 1), for the correlated aggregation, but 0 --- No Need
            GenerateChildren();
        }


        public override void GenerateChildren()
        {
            // need to do all 6 - even if some are empty --- bc of 6x6 correlation matrix

            foreach (SimmRiskClassType riskClass in (SimmRiskClassType[])Enum.GetValues(typeof(SimmRiskClassType)))
            {
                //RiskClass rcNode;

                List<SimmRiskFactor> rfs = LoRiskFactors.Where(rf => rf.Enum4RiskClass == riskClass).ToList();
                if (rfs.Count > 0)
                {
                    Simm2_RiskClass marginElement = new Simm2_RiskClass(rfs, riskClass);
                    Children.Add(marginElement);
                }
            }

            LoRiskFactors = null;
        }

        public override void NodeCompute()
        {
            if (Children.Count > 0)// necessary check because we create all the 4 SIMMProductClasses, but one (or more) could be empty
            {
                // Compute Children
                foreach (var child in Children)
                    child.NodeCompute();

                //Old_Code();
                NodeComputeNodeOnly();
            }
        }
        public override void NodeComputeNodeOnly()
        {
            double[] ordered6RiskClasses_Margin = new double[6] { 0, 0, 0, 0, 0, 0 };
            double[] ordered6RiskClasses_DeltaIM = new double[6] { 0, 0, 0, 0, 0, 0 };
            double[] ordered6RiskClasses_VegaIM = new double[6] { 0, 0, 0, 0, 0, 0 };
            double[] ordered6RiskClasses_CurvIM = new double[6] { 0, 0, 0, 0, 0, 0 };
            double[] ordered6RiskClasses_BaseCorrIM = new double[6] { 0, 0, 0, 0, 0, 0 };

            foreach (NodeMargin child in Children)
            {
                int i = ((int)child.RiskClassEnum);
                ordered6RiskClasses_Margin[i] = child.Margin;
                ordered6RiskClasses_DeltaIM[i] = child.MSIMMDeltaIM;
                ordered6RiskClasses_VegaIM[i] = child.MSIMMVegaIM;
                ordered6RiskClasses_CurvIM[i] = child.MSIMMCurvIM;
                ordered6RiskClasses_BaseCorrIM[i] = child.MSIMMBaseCorrIM;
            }

            Margin = AggregateCorrelatedRiskClasses_New(ordered6RiskClasses_Margin);
            MSIMMDeltaIM = AggregateCorrelatedRiskClasses_New(ordered6RiskClasses_DeltaIM);
            MSIMMVegaIM = AggregateCorrelatedRiskClasses_New(ordered6RiskClasses_VegaIM);
            MSIMMCurvIM = AggregateCorrelatedRiskClasses_New(ordered6RiskClasses_CurvIM);
            MSIMMBaseCorrIM = AggregateCorrelatedRiskClasses_New(ordered6RiskClasses_BaseCorrIM);

            // now we can reduce first level
            ReduceChildren();
        }

        //private void Old_Code()
        //{
        //    if (Children.Count > 0)// necessary check because we create all the 4 SIMMProductClasses, but one (or more) could be empty
        //    {
        //        // Compute Children
        //        foreach (var child in Children)
        //            child.NodeCompute();

        //        // now we can reduce first
        //        ReduceChildren();

        //        List<double> margins = new List<double>();
        //        List<double> margins_delta = new List<double>();
        //        List<double> margins_vega = new List<double>();
        //        List<double> margins_curv = new List<double>();
        //        List<double> margins_basecorr = new List<double>();

        //        foreach (MarginNode child in Children)
        //        {
        //            margins.Add(child.Margin);
        //            margins_delta.Add(child.MSIMMDeltaIM);
        //            margins_vega.Add(child.MSIMMVegaIM);
        //            margins_curv.Add(child.MSIMMCurvIM);
        //            margins_basecorr.Add(child.MSIMMBaseCorrIM);
        //        }

        //        Margin = AggregateCorrelatedRiskClasses(margins);
        //        MSIMMDeltaIM = AggregateCorrelatedRiskClasses(margins_delta);
        //        MSIMMVegaIM = AggregateCorrelatedRiskClasses(margins_vega);
        //        MSIMMCurvIM = AggregateCorrelatedRiskClasses(margins_curv);
        //        MSIMMBaseCorrIM = AggregateCorrelatedRiskClasses(margins_basecorr);

        //        //// now we can reduce first
        //        //ReduceChildren();
        //    }
        //}
        //private double AggregateCorrelatedRiskClasses(List<double> margins)// RiskClassIMsAggregation(double[] order6RiskClassesIM)
        //{
        //    double[] order6RiskClassesIM = new double[6];

        //    for (int i = 0; i < 6; i++)
        //    {
        //        order6RiskClassesIM[i] = margins[i];
        //    }

        //    Matrix<double> RiskClassesCorrMath2 = DenseMatrix.OfArray(Globals.Parameters.RiskClassCorr);

        //    Vector<double> OrderedRisksVECT = DenseVector.OfArray(order6RiskClassesIM);

        //    Matrix<double> OrderedRisksCOLUM = OrderedRisksVECT.ToColumnMatrix();
        //    Matrix<double> OrderedRisksTRANS = OrderedRisksVECT.ToRowMatrix();

        //    var res2 = OrderedRisksTRANS * RiskClassesCorrMath2 * OrderedRisksCOLUM;

        //    double ans = Math.Sqrt(res2[0, 0]);
        //    return ans;
        //}

        private double AggregateCorrelatedRiskClasses_New(double[] order6RiskClassesIM)// RiskClassIMsAggregation(double[] order6RiskClassesIM)
        {
            Matrix<double> RiskClassesCorrMath2 = DenseMatrix.OfArray(Globals.Parameters.RiskClassCorr);

            Vector<double> OrderedRisksVECT = DenseVector.OfArray(order6RiskClassesIM);

            Matrix<double> OrderedRisksCOLUM = OrderedRisksVECT.ToColumnMatrix();
            Matrix<double> OrderedRisksTRANS = OrderedRisksVECT.ToRowMatrix();

            var res2 = OrderedRisksTRANS * RiskClassesCorrMath2 * OrderedRisksCOLUM;

            double ans = Math.Sqrt(res2[0, 0]);
            return ans;
        }

    }
}

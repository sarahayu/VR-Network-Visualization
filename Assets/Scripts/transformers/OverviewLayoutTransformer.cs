namespace VidiGraph
{
    public class OverviewLayoutTransformer : NetworkContextTransformer
    {
        public override void Initialize(NetworkGlobal networkGlobal, NetworkContext networkContext)
        {
        }

        public override void ApplyTransformation()
        {
        }

        public override TransformInterpolator GetInterpolator()
        {
            return new OverviewLayoutInterpolator();
        }
    }

    public class OverviewLayoutInterpolator : TransformInterpolator
    {
        public override void Interpolate(float t)
        {
            // leave empty
        }
    }
}
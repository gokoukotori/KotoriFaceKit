namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ExpressionOverrideComponent : ExpressionDataSourceComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Expression Override";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public ExpressionDataSourceComponent? TargetBase = null;
    }
}

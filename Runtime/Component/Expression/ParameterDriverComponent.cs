namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPath)]
    public class ParameterDriverComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = $"{FaceTuneConstants.ComponentPrefix} Parameter Driver";
        internal const string MenuPath = BasePath + "/" + Expression + "/" + ComponentName;

        public bool LocalOnly = false;
        public List<ParameterDriverOperation> Operations = new();

        internal ParameterDriverSettings ToParameterDriver()
        {
            return new ParameterDriverSettings(LocalOnly, Operations);
        }
    }
}

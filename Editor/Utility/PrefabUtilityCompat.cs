namespace Aoyon.FaceTune
{
    internal static class PrefabUtilityCompat
    {
        private static readonly Type PrefabUtilityType = typeof(UnityEditor.PrefabUtility);

        public static void UnpackPrefabInstance(GameObject instance, PrefabUnpackMode unpackMode, InteractionMode action)
        {
            var unpackArguments = new object[] { instance, unpackMode, action };
            if (TryInvokeStaticMethod("UnpackPrefabInstance", unpackArguments))
            {
                return;
            }

            if (TryInvokeStaticMethod("UnpackPrefabInstanceAndReturnNewOutermostRoots", unpackArguments))
            {
                return;
            }

            if (TryInvokeStaticMethod("DisconnectPrefabInstance", new object[] { instance }))
            {
                return;
            }

            throw new MissingMethodException(PrefabUtilityType.FullName, "UnpackPrefabInstance");
        }

        private static bool TryInvokeStaticMethod(string methodName, object[] arguments)
        {
            foreach (var method in PrefabUtilityType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (method.Name != methodName) continue;

                var parameters = method.GetParameters();
                if (parameters.Length != arguments.Length) continue;
                if (!parameters.Zip(arguments, (parameter, argument) => parameter.ParameterType.IsInstanceOfType(argument)).All(x => x)) continue;

                method.Invoke(null, arguments);
                return true;
            }

            return false;
        }
    }
}

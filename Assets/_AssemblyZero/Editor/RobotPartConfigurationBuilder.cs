using UnityEditor;
using UnityEngine;
namespace AssemblyZero.Unity.Editor
{
    public static class RobotPartConfigurationBuilder
    {
        public const string Root = "Assets/_AssemblyZero/EP02";
        public static RobotPartDefinition[] Build(Material carrierMaterial)
        {
            var audit = RobotPartAudit.Inspect(); var bounds = audit[0].CommonBounds; for (var i = 1; i < audit.Length; i++) bounds.Encapsulate(audit[i].CommonBounds);
            var commonScale = 2.8f / bounds.size.y; var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var parts = new RobotPartDefinition[6]; var palette = new[] { Color.cyan, new Color(1, .65f, .16f), new Color(.5f, .8f, 1), new Color(1, .45f, .6f), new Color(.6f, 1, .5f), new Color(.7f, .5f, 1) };
            var carrierPath = Root + "/Prefabs/StandardCarrier.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(carrierPath) == null) { var carrier = EP02SceneBuilder.Primitive("StandardCarrier", PrimitiveType.Cube, null, new Vector3(0, .07f, 0), new Vector3(.72f, .12f, .66f), carrierMaterial); PrefabUtility.SaveAsPrefabAsset(carrier, carrierPath); Object.DestroyImmediate(carrier); }
            for (var i = 0; i < parts.Length; i++)
            {
                var p = audit[i]; var name = RobotPartAudit.Names[i]; var path = Root + "/Configs/" + name + ".asset";
                var def = AssetDatabase.LoadAssetAtPath<RobotPartDefinition>(path); var isNew = def == null;
                if (isNew)
                {
                    def = ScriptableObject.CreateInstance<RobotPartDefinition>(); def.PartTypeId = 2 + i; def.SocketId = i; def.PartName = name; def.SourcePrefab = p.Prefab; def.Color = palette[i];
                    var assemblyRotation = p.Registration.rotation;
                    def.AssemblyPose = new PartPose(assemblyRotation * (-p.LocalBounds.center * commonScale), assemblyRotation.eulerAngles, commonScale);
                    def.GripPose = def.AssemblyPose;
                    // Flat transport pose, uniformly fitted to a pallet. Final assembly uses ONE common scale.
                    var flat = Quaternion.Euler(90, 0, 0); var beltScale = .62f / Mathf.Max(p.LocalBounds.size.x, p.LocalBounds.size.y, p.LocalBounds.size.z);
                    def.BeltPose = new PartPose(flat * (-p.LocalBounds.center * beltScale) + Vector3.up * .24f, flat.eulerAngles, beltScale);
                    def.AssemblySocketPosition = (p.CommonBounds.center - origin) * commonScale + Vector3.up * .75f;
                    AssetDatabase.CreateAsset(def, path);
                }
                if (def.WrapperPrefab == null)
                {
                    var root = new GameObject(name + " CarrierRoot"); var payload = new GameObject("PayloadRoot"); payload.transform.SetParent(root.transform, false);
                    var visual = new GameObject("PartVisualAnchor"); visual.transform.SetParent(payload.transform, false);
                    var source = (GameObject)PrefabUtility.InstantiatePrefab(def.SourcePrefab); source.transform.SetParent(visual.transform, false); def.BeltPose.Apply(source.transform);
                    var tray = EP02SceneBuilder.Primitive("Carrier", PrimitiveType.Cube, root.transform, new Vector3(0, .07f, 0), new Vector3(.72f, .12f, .66f), carrierMaterial);
                    var anchor = root.AddComponent<PartVisualAnchor>(); anchor.PayloadRoot = payload.transform; anchor.SourceRoot = source.transform; anchor.Carrier = tray; anchor.Definition = def;
                    def.WrapperPrefab = PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/" + name + "Carrier.prefab"); Object.DestroyImmediate(root);
                }
                EditorUtility.SetDirty(def); parts[i] = def;
            }
            AssetDatabase.SaveAssets(); return parts;
        }
    }
}

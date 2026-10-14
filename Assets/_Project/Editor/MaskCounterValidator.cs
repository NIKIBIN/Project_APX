using System.Collections.Generic;
using System.Linq;
using APX.Combat;
using APX.Core;
using APX.Enemies;
using APX.Level;
using APX.Masks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace APX.EditorTools
{
    /// <summary>
    /// Every obstacle must answer to exactly one mask, so the player has to wear that mask to get past it. Lists the
    /// obstacles (hazards, enemies, doors, moving and timed traps) that more than one mask can affect, through
    /// mask responders on the obstacle, its parents or its children. Spike traps and other danger sources on the
    /// obstacle count as "Segundo canal" (its danger sense times them).
    /// </summary>
    static class MaskCounterValidator
    {
        const string PrefabFolder = "Assets/_Project/Prefabs";

        [MenuItem("APX/Validate Mask Counters")]
        static void Validate()
        {
            int checkedCount = 0;
            int problemCount = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab != null)
                    Check(prefab, ref checkedCount, ref problemCount);
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                foreach (GameObject root in scene.GetRootGameObjects())
                    Check(root, ref checkedCount, ref problemCount);
            }

            string summary = $"Mask counters: {checkedCount} obstacles checked, {problemCount} answer to more than one mask.";
            if (problemCount > 0)
                Debug.LogWarning(summary);
            else
                Debug.Log(summary);
        }

        static void Check(GameObject root, ref int checkedCount, ref int problemCount)
        {
            HashSet<GameObject> obstacles = FindObstacles(root);
            foreach (GameObject obstacle in obstacles)
            {
                // A part of a bigger obstacle (e.g. a trap's hurtbox) is checked with it.
                if (HasObstacleAncestor(obstacle, obstacles))
                    continue;

                checkedCount++;
                List<MaskType> masks = CountersOf(obstacle);
                if (masks.Count <= 1)
                    continue;

                problemCount++;
                Debug.LogWarning($"{Describe(obstacle)} answers to {string.Join(" and ", masks)}. " +
                                 "An obstacle should need exactly one mask.", obstacle);
            }
        }

        static HashSet<GameObject> FindObstacles(GameObject root)
        {
            var obstacles = new HashSet<GameObject>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component is DamageOnContact or EnemyBase or StoneDoor or SpikeTrap or WaypointMover)
                    obstacles.Add(component.gameObject);
            }

            return obstacles;
        }

        static bool HasObstacleAncestor(GameObject obstacle, HashSet<GameObject> obstacles)
        {
            for (Transform parent = obstacle.transform.parent; parent != null; parent = parent.parent)
            {
                if (obstacles.Contains(parent.gameObject))
                    return true;
            }

            return false;
        }

        static List<MaskType> CountersOf(GameObject obstacle)
        {
            var masks = new HashSet<MaskType>();
            // Responders affect their own children (time sources and renderers/colliders are looked up downwards).
            foreach (MaskResponder responder in obstacle.GetComponentsInParent<MaskResponder>(true))
                masks.Add(responder.Mask);
            foreach (MaskResponder responder in obstacle.GetComponentsInChildren<MaskResponder>(true))
                masks.Add(responder.Mask);
            // The danger sense ("Segundo canal") times spike traps and other danger sources on the obstacle itself.
            if (obstacle.GetComponent<SpikeTrap>() != null || obstacle.GetComponent<IDangerSource>() != null)
                masks.Add(MaskType.SecondChannel);

            return masks.OrderBy(mask => mask).ToList();
        }

        static string Describe(GameObject obstacle)
        {
            string path = obstacle.name;
            for (Transform parent = obstacle.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;

            string container = obstacle.scene.IsValid() ? obstacle.scene.name : AssetDatabase.GetAssetPath(obstacle);
            return $"{container}: {path}";
        }
    }
}

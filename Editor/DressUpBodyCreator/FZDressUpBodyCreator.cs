using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using EUI = FZTools.EditorUtils.UI;
using ELayout = FZTools.EditorUtils.Layout;
using System.IO;
using System;
using VRC.SDK3.Dynamics.PhysBone.Components;
namespace FZTools
{
    /// <summary>
    /// 改変済みのアバターや未改変状態のアバターから着せ替え用素体のPrefabを作成します
    /// Modular Avatar使用推奨
    /// </summary>
    public class FZDressUpBodyCreator : EditorWindow
    {
        [SerializeField]
        private GameObject avatar;
        [SerializeField]
        private FZDressUpBodyConstants dressUpBodyConstants;
        // [SerializeField]
        // public List<string> dressUpBodyNames;
        // [SerializeField]
        // public List<string> shrinkShapeNames;

        private string TargetAvatarName => avatar?.name;
        private string PrefabOutputPath => $"{AssetUtils.OutputRootPath(TargetAvatarName)}/Prefab";
        private List<string> MeshNames => avatarMesheRenderers.Select(m => m.name).ToList();
        private List<string> ClothesObjectNames => clothAndAccessoryRootObjectPaths.Select(path => avatar.transform.Find(path.Replace($"{avatar.name}/", "")).name).ToList();
        private List<string> PhysBoneNames => physBones.Select(pb => pb.name).ToList();

        private List<Renderer> avatarMesheRenderers;
        List<string> clothAndAccessoryRootObjectPaths;
        private List<VRCPhysBone> physBones;
        private bool isDeleteRoot;
        private int processingMode;
        private Dictionary<string, bool> meshRendererEnabled = new Dictionary<string, bool>();
        private Dictionary<string, bool> meshRootObjEnabled = new Dictionary<string, bool>();
        private Dictionary<string, bool> physBonesEnabled = new Dictionary<string, bool>();
        Vector2 layoutRootScrollPos;
        Vector2 meshesScrollPos;
        Vector2 gameObjectsScrollPos;
        Vector2 physBoneScrollPos;
        bool isFirst = true;


        /// <summary>
        /// 着想元：2023/07/21 キセナイデネ　Kashiwa_さん,samehadamaru
        /// ジェバンニ（gfool6）がだいたい5時間くらいで初版を完成させました
        /// </summary>
        [MenuItem("FZTools/ヌイデネ(仮)")]
        private static void OpenWindow()
        {
            var window = GetWindow<FZDressUpBodyCreator>("ヌイデネ(仮)");
        }

        SerializedObject serializedObject;
        private void OnGUI()
        {
            ELayout.Horizontal(() =>
                {
                    EUI.Space();
                    ELayout.Scroll(ref layoutRootScrollPos, () =>
                    {
                        ELayout.Vertical(() =>
                        {
                            MissingDressUpBodyConstantsView();
                            BaseUI();
                            if (avatar == null) return;
                            InitVariable(avatar);
                            CreateSelector("残したいアバター本体のメッシュ", ref meshesScrollPos, MeshNames, meshRendererEnabled);
                            if (isFirst)
                            {
                                MeshNames.ForEach(itemName =>
                                {
                                    dressUpBodyConstants.DressUpBodyNames.ForEach(n =>
                                    {
                                        if (itemName.ToLower().Contains(n))
                                        {
                                            meshRendererEnabled[itemName] = true;
                                            return;
                                        }
                                    });
                                });
                                isFirst = false;
                            }
                            CreateSelector("残したい着せ替えアイテム", ref gameObjectsScrollPos, ClothesObjectNames, meshRootObjEnabled);
                            CreateSelector("残したいPhysBones", ref physBoneScrollPos, PhysBoneNames, physBonesEnabled);
                            // TODO PBCの追加　
                            EUI.Space(2);
                            EUI.Button("追い剥ぎ", Bandit);
                            EUI.Space();
                        });
                    });
                });
        }

        private void MissingDressUpBodyConstantsView()
        {
            if (dressUpBodyConstants == null)
            {
                dressUpBodyConstants = AssetDatabase.LoadAssetAtPath<FZDressUpBodyConstants>(AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:FZDressUpBodyConstants").FirstOrDefault()));
                if (dressUpBodyConstants == null)
                {
                    EUI.ErrorBox("素体メッシュ名リストが見つかりませんでした\n新規に作成する場合はOKを押してください");
                    EUI.Button("OK", CreateFZDressUpBodyConstants);
                    return;
                }
                // dressUpBodyNames = dressUpBodyConstants.DressUpBodyNames;
                // shrinkShapeNames = dressUpBodyConstants.ShrinkShapeNames;
            }
        }

        private void BaseUI()
        {
            EUI.Space(2);
            EUI.Label("Target Avatar");
            EUI.Space();
            EUI.ChangeCheck(
                () => { EUI.ObjectField<GameObject>(ref avatar); },
                () =>
                {
                    meshRendererEnabled = new Dictionary<string, bool>();
                    meshRootObjEnabled = new Dictionary<string, bool>();
                    physBonesEnabled = new Dictionary<string, bool>();
                    isFirst = false;
                }
            );
            EUI.Space(2);
            EUI.InfoBox("セットしたアバターから服を消して着せ替え用素体を作成します");
            EUI.Space(2);
            EUI.RadioButton(ref processingMode, new List<string>() { "非表示にする", "消し去る" }.ToArray());
            EUI.Space(2);
        }

        // private void DressUpBodyConstLists()
        // {
        //     EUI.Space(2);
        //     EUI.Label("オプション");
        //     var serializedObject = new SerializedObject(this);
        //     EUI.SerializedPropertyField("dressUpBodyNames", serializedObject);
        //     EUI.SerializedPropertyField("shrinkShapeNames", serializedObject);
        //     EUI.Space(2);
        // }

        private void CreateSelector(string selectorDetail, ref Vector2 scrollPos, List<string> itemNameList, Dictionary<string, bool> enabledsDict)
        {
            EUI.Label(selectorDetail);
            ELayout.Scroll(ref scrollPos, () =>
            {
                itemNameList.ForEach(itemName =>
                {
                    if (!enabledsDict.ContainsKey(itemName))
                    {
                        enabledsDict.Add(itemName, false);
                    }

                    ELayout.Horizontal(() =>
                    {
                        // var temp = enabledsDict[itemName];
                        // EUI.ToggleWithLabel(ref temp, itemName);
                        // enabledsDict[itemName] = temp;
                        enabledsDict[itemName] = EditorGUILayout.ToggleLeft(itemName, enabledsDict[itemName]);
                    });
                });
            });
            EUI.Space(2);
        }

        private void InitVariable(GameObject avatrarObject)
        {
            avatarMesheRenderers = avatrarObject.GetComponentsInChildren<Renderer>(true)
                                                .Where(n => n.gameObject.GetGameObjectPath().Split('/').Count() <= 2)
                                                .ToList();
            clothAndAccessoryRootObjectPaths = avatrarObject.GetComponentsInChildren<Renderer>(true)
                                                        .Select(n => n.gameObject.GetGameObjectPath().Split('/'))
                                                        .Where(n => n.Count() > 2)
                                                        .Select(n => string.Join("/", n.Take(n.Length - 1)))
                                                        .Distinct().ToList();
            physBones = avatrarObject.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true).ToList();
        }

        private void Bandit()
        {
            GameObject dressupDoll;
            AssetUtils.CreateDirectoryRecursive(PrefabOutputPath);
            if (PrefabUtility.GetPrefabAssetType(avatar) == PrefabAssetType.NotAPrefab)
            {
                dressupDoll = avatar;
            }
            else
            {
                var prefabFilePath = $"{PrefabOutputPath}/{avatar.name}_Kisekae.prefab";
                // TODO prefabの"prefab"から取得するよう変更する
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetUtils.FindAssetPathFromObjectFileName($"{avatar.name}.prefab"));
                dressupDoll = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                var pType = PrefabUtility.GetPrefabAssetType(dressupDoll);
                if (pType != PrefabAssetType.NotAPrefab)
                {
                    PrefabUtility.UnpackPrefabInstance(dressupDoll, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                }
            }
            dressupDoll.SetActive(true);
            InitVariable(dressupDoll);

            // 動作モード別非表示化
            switch (processingMode)
            {
                case 0:
                    DesKaisersNeueKleider(dressupDoll);
                    DisablePhysBones(dressupDoll);
                    break;
                case 1:
                    Rajomon(dressupDoll);
                    RemovePhysBones(dressupDoll);
                    break;
                default:
                    break;
            }

            // シュリンクの復元
            // 消えてるかもしれないので再セット
            var meshRenderers = dressupDoll.GetComponentsInChildren<Renderer>(true).ToList();
            meshRenderers.Where(m => m.gameObject.activeSelf && m is SkinnedMeshRenderer).ToList().ForEach(m =>
            {
                var meshRenderer = ((SkinnedMeshRenderer)m);
                for (int i = 0; i < meshRenderer.sharedMesh.blendShapeCount; i++)
                {
                    dressUpBodyConstants.ShrinkShapeNames.ForEach(s =>
                    {
                        if (meshRenderer.sharedMesh.GetBlendShapeName(i).ToLower().Contains(s))
                        {
                            meshRenderer.SetBlendShapeWeight(i, 0);
                        }
                    });
                }
            });



            PrefabUtility.SaveAsPrefabAssetAndConnect(dressupDoll, $"{PrefabOutputPath}/{avatar.name}_Kisekae.prefab", InteractionMode.AutomatedAction);
            DestroyImmediate(dressupDoll);
        }

        private void DesKaisersNeueKleider(GameObject avatarObj)
        {
            var meshRenderers = avatarObj.GetComponentsInChildren<Renderer>(true).Where(n => n.gameObject.GetGameObjectPath().Split('/').Count() <= 2).ToList();
            meshRenderers.ForEach(meshRenderer =>
            {
                if (!meshRendererEnabled.ContainsKey(meshRenderer.name))
                {
                    return;
                }
                meshRenderer.gameObject.SetActive(meshRendererEnabled[meshRenderer.name]);
                meshRenderer.enabled = meshRendererEnabled[meshRenderer.name];
                if (!meshRendererEnabled[meshRenderer.name])
                {
                    meshRenderer.gameObject.tag = "EditorOnly";
                }
            });

            var gameObjects = clothAndAccessoryRootObjectPaths.Select(go => avatarObj.transform.Find(go.Replace($"{avatar.name}/", "")).gameObject).ToList();
            gameObjects.ForEach(gameObject =>
            {
                if (!meshRootObjEnabled.ContainsKey(gameObject.name))
                {
                    return;
                }
                gameObject.SetActive(meshRootObjEnabled[gameObject.name]);
                if (!meshRootObjEnabled[gameObject.name])
                {
                    gameObject.tag = "EditorOnly";
                }
            });
        }

        private void Rajomon(GameObject avatarObj)
        {
            var meshRenderers = avatarObj.GetComponentsInChildren<Renderer>(true).Where(n => n.gameObject.GetGameObjectPath().Split('/').Count() <= 2).ToList();
            meshRenderers.ForEach(meshRenderer =>
            {
                if (!meshRendererEnabled.ContainsKey(meshRenderer.name))
                {
                    return;
                }
                if (!meshRendererEnabled[meshRenderer.name])
                {
                    DestroyImmediate(meshRenderer.gameObject);
                }
            });

            var gameObjects = clothAndAccessoryRootObjectPaths.Select(go => avatarObj.transform.Find(go.Replace($"{avatar.name}/", "")).gameObject).ToList();
            gameObjects.ForEach(gameObject =>
            {
                if (!meshRootObjEnabled.ContainsKey(gameObject.name))
                {
                    return;
                }
                if (!meshRootObjEnabled[gameObject.name])
                {
                    DestroyImmediate(gameObject);
                }
            });
        }

        private void DisablePhysBones(GameObject avatarObj)
        {
            var physBones = avatarObj.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true).ToList();
            physBones.ForEach(physBone =>
            {
                if (!physBonesEnabled.ContainsKey(physBone.name))
                {
                    return;
                }
                if (!physBonesEnabled[physBone.name])
                {
                    physBone.enabled = false;
                }
            });
        }

        private void RemovePhysBones(GameObject avatarObj)
        {
            var physBones = avatarObj.GetComponentsInChildren<VRC.SDK3.Dynamics.PhysBone.Components.VRCPhysBone>(true).ToList();
            physBones.ForEach(physBone =>
            {
                if (!physBonesEnabled.ContainsKey(physBone.name))
                {
                    return;
                }
                if (!physBonesEnabled[physBone.name])
                {
                    GameObject.DestroyImmediate(physBone);
                }
            });
        }

        private void CreateFZDressUpBodyConstants()
        {
            var dubc = ScriptableObject.CreateInstance<FZDressUpBodyConstants>();
            var thisPath = Directory.GetFiles("Packages", "*", System.IO.SearchOption.AllDirectories)
                                    .FirstOrDefault(path => System.IO.Path.GetFileName(path) == "FZDressUpBodyCreator.cs")
                                    .convertWinPath2Path()
                                    .Replace("FZDressUpBodyCreator.cs", "");
            AssetUtils.CreateAsset(dubc, $"{thisPath}/FZDressUpBodyConstants.asset");
        }
    }
}
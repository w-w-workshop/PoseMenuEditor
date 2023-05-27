using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using UnityEngine.Animations;
using UnityEngine.Playables;
using HakuroEditor.PoseMenuEditor.Preview;
using HakuroEditor.PoseMenuEditor.ObjectData;

namespace HakuroEditor.PoseMenuEditor
{
    public class PoseMenuEditorWindow : EditorWindow
    {
        //アバター変数
        private VRCAvatarDescriptor avatarDescriptor;
        private GameObject AvatarObject, fromAvatarObject;
        private AnimatorControllerLayer poseLayer;
        private VRCExpressionParameters exParam;
        private VRCExpressionsMenu exMenu, poseMenu;

        //文字列変数
        private string parameterName = "HakuroPoseParameter";
        private string layerName = "HakuroPoseLayer";
        private string folderPath, fileName = "PoseMenu";
        private string stateName_WAIT = "Wait", stateName_START = "START", stateName_END = "END";
        private string defaultStateName = "Pose";

        //ポーズリスト
        private List<List<ScriptableObjectVRCPoseState>> VRCAnimatorStatelist = new List<List<ScriptableObjectVRCPoseState>>();
        private AnimatorState state_End, state_Wait, state_Start;
        private readonly List<AnimationClip> _animationClips = new List<AnimationClip>();

        private UnityEditorInternal.ReorderableList VRCAnimatorState_ReorderableList;
        private List<string> _tabToggles = new List<string>{ "Menu1", "Menu2", "Menu3", "Menu4", "Menu5", "Menu6", "Menu7", "Menu8" };
        private int _tabIndex;

        //ポーズ再生
        private PlayableGraph playableGraph;
        private PreviewScene _previewScene;
        private bool didInitialize;
        private GameObject AvatarObject_Preview;
        private string playingAnimationClipName;

        //メニューアイコン作成フラグ
        private bool createMenuIcon = true;

        //設定保存用変数
        public string assetFolderPath = "Assets/PoseMenuEditor/";
        private SerializedObject serializedObject;
        private string dataObjectPath;
        public SetData obj;
        private AnimationClip defaultAnimationClip;

        private void OnEnable()
        {
            //設定読み込み処理
            didInitialize = false;
            dataObjectPath = assetFolderPath + typeof(SetData) + ".asset";
            obj = AssetDatabase.LoadAssetAtPath<SetData>(dataObjectPath);
            if (obj == null)
            {
                obj = ScriptableObject.CreateInstance<SetData>();
            }
            serializedObject = new SerializedObject(obj);
            if (!System.IO.File.Exists(dataObjectPath))
            {
                AssetDatabase.CreateAsset(obj, dataObjectPath);
            }

            for (int i = 0; i < 8; i++)
            {
                VRCAnimatorStatelist.Add(new List<ScriptableObjectVRCPoseState>());
            }

            defaultAnimationClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetFolderPath + "default.anim");
        }

        [MenuItem("HakuroEditor/PoseMenuEditor")]
        public static void Create()
        {
            GetWindow<PoseMenuEditorWindow>("PoseMenuEditor");
        }

        void OnGUI()
        {
            //アバター設定欄
            EditorGUILayout.BeginHorizontal();
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.LabelField("再生モード中は操作できません。");
                return;
            }
            GameObject AvatarObject_Sub = (GameObject)EditorGUILayout.ObjectField("Avatar", AvatarObject, typeof(GameObject), true, GUILayout.Width(position.width- 10));
            EditorGUILayout.EndHorizontal();

            if (AvatarObject_Sub != null && AvatarObject != AvatarObject_Sub && CheckAvatarDescriptor(AvatarObject_Sub))
            {
                AvatarObject = AvatarObject_Sub;
                didInitialize = false;
            }

            //アバターが設定されている場合のみ項目を表示する
            if (AvatarObject != null)
            {

                //ウィンドウオープン時のみPoseLayerがあればリスト化する
                if (!didInitialize)
                {
                    InitializeIfNeeded();
                    //カメラを初期化する
                    _previewScene.Camera.transform.position = obj.cameraPosition;
                    _previewScene.Camera.transform.rotation = obj.cameraRotation;
                    _previewScene.Camera.fieldOfView = obj.cameraFieldOfView;

                    SearchPoseStateList(AvatarObject);
                }

                EditorGUILayout.BeginHorizontal();


                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();

                if(position.width > (int)position.height)
                {
                    _previewScene.RenderTextureSize = new Vector2Int((int)position.width, (int)position.width);
                }
                else
                {
                    _previewScene.RenderTextureSize = new Vector2Int((int)position.height, (int)position.height);
                }
                _previewScene.Render();

                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 20), GUILayout.Height(position.height / 2));
                EditorGUILayout.LabelField("再生中アニメーション：" + playingAnimationClipName);
                EditorGUILayout.LabelField(new GUIContent(_previewScene.RenderTexture), GUILayout.Width(position.width / 2), GUILayout.Height(position.height / 2));

                EditorGUILayout.EndVertical();


                EditorGUILayout.BeginVertical(GUILayout.Width(position.width / 2 - 20), GUILayout.Height(position.height / 2 ));
                _previewScene.Camera.backgroundColor = EditorGUILayout.ColorField("プレビューカメラ背景色", _previewScene.Camera.backgroundColor);
                _previewScene.Camera.transform.position = EditorGUILayout.Vector3Field("position", _previewScene.Camera.transform.position);
                _previewScene.Camera.transform.rotation = Quaternion.Euler(EditorGUILayout.Vector3Field("rotation", _previewScene.Camera.transform.rotation.eulerAngles));
                _previewScene.Camera.fieldOfView = EditorGUILayout.Slider("Field of View", _previewScene.Camera.fieldOfView, 0, 180);
                if (GUILayout.Button("プレビューカメラ位置初期化"))
                {
                    _previewScene.Camera.transform.position = new Vector3(0, 1, 2);
                    _previewScene.Camera.transform.rotation = new Quaternion(0, 180, 0, 0);
                    _previewScene.Camera.fieldOfView = 60;
                }
                if (GUILayout.Button("プレビューカメラ位置保存"))
                {
                    obj.cameraRotation = _previewScene.Camera.transform.rotation;
                    obj.cameraPosition = _previewScene.Camera.transform.position;
                    obj.cameraFieldOfView = _previewScene.Camera.fieldOfView;
                    EditorUtility.SetDirty(obj);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("ポーズを別アバターから取得する");
                fromAvatarObject = (GameObject)EditorGUILayout.ObjectField("Avatar",fromAvatarObject, typeof(GameObject),true);
                if (GUILayout.Button("取得"))
                {
                    if (fromAvatarObject != null)
                    {
                        if(CheckAvatarDescriptor(fromAvatarObject))
                        {
                            SearchPoseStateList(fromAvatarObject);
                        }
                        fromAvatarObject = null;
                    }
                }

                EditorGUILayout.Space();
                EditorGUILayout.Space();

                createMenuIcon = GUILayout.Toggle(createMenuIcon, "メニューアイコンを作成する");

                if (GUILayout.Button("適用"))
                {
                    SetupPoseSettings();
                }
                if (GUILayout.Button("閉じる"))
                {
                    Close();
                }
                

                //AnimationClipを複数を取得するエリア表示
                if (DragAndDropAreaUtility.GetObjects(_animationClips, "AnimationClip Drag&Drop"))
                {
                    foreach (var _animationClip in _animationClips)
                    {
                        if (VRCAnimatorStatelist[_tabIndex].Count > 7) break;
                        VRCAnimatorStatelist[_tabIndex].Add(CreateInstance<ScriptableObjectVRCPoseState>());
                        VRCAnimatorStatelist[_tabIndex][VRCAnimatorStatelist[_tabIndex].Count - 1]._animationClipValue = _animationClip;
                        string menuName = _animationClip.name;
                        VRCAnimatorStatelist[_tabIndex][VRCAnimatorStatelist[_tabIndex].Count - 1]._MenuName = "";
                        VRCAnimatorStatelist[_tabIndex][VRCAnimatorStatelist[_tabIndex].Count - 1]._MenuName = getNotSameName(menuName);
                    }
                    _animationClips.Clear();

                    InitializeReorderableList();
                }
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();

                //メニュータブ
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Width((int)position.width)))
                {
                    
                    int _tabIndex_sub = GUILayout.Toolbar(_tabIndex, _tabToggles.ToArray(), new GUIStyle(EditorStyles.toolbarButton), GUI.ToolbarButtonSize.Fixed);

                    if (_tabIndex != _tabIndex_sub)
                    {
                        GUI.FocusControl("");
                        _tabIndex = _tabIndex_sub;
                        InitializeReorderableList();
                    }
                }
                
                EditorGUILayout.BeginVertical(GUILayout.Width(position.width - 20), GUILayout.Height(position.height / 2));
                EditorGUILayout.Space();
                VRCAnimatorState_ReorderableList.DoLayoutList();

                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorGUILayout.LabelField("アバターを設定してください。");
            }
        }

        private bool CheckAvatarDescriptor(GameObject gameObject)
        {
            VRCAvatarDescriptor descriptor = gameObject.GetComponent<VRCAvatarDescriptor>();
            UnityEditor.Animations.AnimatorController controller = AvatarUtils.GetAnimator(gameObject.GetComponent<VRCAvatarDescriptor>(), AvatarUtils.ACTIONLAYER);
            if (controller == null)
            {
                if (EditorUtility.DisplayDialog("Error", "ActionLayerが設定されていません。", "OK")) ;
                return false;
            }

            VRCExpressionsMenu menu = AvatarUtils.GetExpMenu(descriptor);
            if (menu == null)
            {
                if (EditorUtility.DisplayDialog("Error", "ExpressionMenuが設定されていません。", "OK")) ;
                return false;
            }
            VRCExpressionParameters param = AvatarUtils.GetExpPara(descriptor);
            if (param == null)
            {
                if (EditorUtility.DisplayDialog("Error", "ExpressionParameterが設定されていません。", "OK")) ;
                return false;
            }

            return true;
        }

        //・初期設定
        //１．ExParameterにパラメータを追加
        //２．ExMenuに新規SubMenu追加
        //３．ActionLayerにパラメータを追加
        //４．ActionLayerで専用レイヤーを作成
        private void SetupPoseSettings()
        {
            avatarDescriptor = AvatarObject.GetComponent<VRCAvatarDescriptor>();
            exMenu = AvatarUtils.GetExpMenu(avatarDescriptor);
            exParam = AvatarUtils.GetExpPara(avatarDescriptor);

            //パラメータの追加チェック
            if ((AvatarUtils.CheckExpParaMultiple(exParam, VRCExpressionParameters.ValueType.Int, 1) == false
                && AvatarUtils.FindExpPara(exParam, parameterName, VRCExpressionParameters.ValueType.Int) == null))
            {
                Debug.Log("パラメータが追加できません。");
                if (EditorUtility.DisplayDialog("Error", "ExpressionParameterにパラメータが追加できません。\r\n" + "不要なパラメータを削除してください。", "OK"))
                {
                }
                return;
            }

            //メニューの追加チェック
            if (AvatarUtils.CheckMenuMultiple(exMenu, 1) == false
                && AvatarUtils.CheckExpSubMenu(exMenu, "PoseMenu", VRCExpressionsMenu.Control.ControlType.SubMenu) == false)
            {
                Debug.Log("サブメニューが追加できません。");
                if (EditorUtility.DisplayDialog("Error", "ExpressionMenuにサブメニューが追加できません。\r\n" + "不要なメニューを削除してください。", "OK"))
                {
                }
                return;
            }

            //ExParameterにパラメータを追加
            AvatarUtils.AddExpPara(exParam, parameterName,VRCExpressionParameters.ValueType.Int,false,0,false);
            folderPath = Path.GetDirectoryName(AssetDatabase.GetAssetPath(exMenu));
            
            bool fileExists = File.Exists(folderPath + @"\" + fileName + ".asset");

            //ExMenuに新規SubMenu追加
            if(fileExists)
            {
                AssetDatabase.DeleteAsset(folderPath + @"\" + fileName + ".asset");
            }
            poseMenu = AvatarUtils.CreateExpMenu(folderPath + @"\", fileName + ".asset");
            AvatarUtils.AddExpSubMenu(exMenu, "PoseMenu", VRCExpressionsMenu.Control.ControlType.SubMenu, poseMenu);

            float count = 0;
            float progress;
            float stateCount = 0;
            for (int i = 0; i < VRCAnimatorStatelist.Count; i++)
            {
                stateCount += VRCAnimatorStatelist[i].Count;
            }
            for (int i = 0; i < VRCAnimatorStatelist.Count; i++)
            {
                if (VRCAnimatorStatelist[i].Count == 0) continue;
                fileExists = File.Exists(folderPath + @"\" + fileName +"_" + i.ToString() + ".asset");
                if (fileExists)
                {
                    AssetDatabase.DeleteAsset(folderPath + @"\" + fileName + "_" + i.ToString() + ".asset");
                }
                VRCExpressionsMenu subMenu = AvatarUtils.CreateExpMenu(folderPath + @"\", fileName + "_" + i.ToString() + ".asset");
                AvatarUtils.AddExpSubMenu(poseMenu, "PoseMenu" + (i + 1).ToString(), VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu);
                for (int t = 0; t < VRCAnimatorStatelist[i].Count; t++)
                {
                    Texture2D icon = null;
                    if (createMenuIcon)
                    {
                        SetupPlayableGraph(VRCAnimatorStatelist[i][t]._animationClipValue);
                        PlayAnimation();
                        _previewScene.Render();
                        saveExMenuIcon(folderPath + @"\icon", VRCAnimatorStatelist[i][t]._MenuName, _previewScene.RenderTexture);
                        icon = getExMenuIcon(folderPath + @"\icon", VRCAnimatorStatelist[i][t]._MenuName, _previewScene.RenderTexture);
                    }

                    AvatarUtils.AddExpMenu(subMenu, VRCAnimatorStatelist[i][t]._MenuName, VRCExpressionsMenu.Control.ControlType.Toggle, parameterName, i * 8 + t+ 1, null, icon);

                    count += 1;
                    progress = (float)(count / stateCount);
                    EditorUtility.DisplayProgressBar("処理中", (progress * 100).ToString("F2") + "%", progress);
                    
                }

                EditorUtility.SetDirty(subMenu);
            }

            UnityEditor.Animations.AnimatorController controller = AvatarUtils.GetAnimator(AvatarObject.GetComponent<VRCAvatarDescriptor>(), AvatarUtils.ACTIONLAYER);
            //ActionLayerにパラメータを追加
            AvatarUtils.CreateAnimatorParameter(controller, parameterName, AnimatorControllerParameterType.Int);

            //ActionLayerで専用レイヤーを作成
            poseLayer = AvatarUtils.CreateAnimatorLayer(controller, layerName,true);

            SetupAnimatorLayer();

            EditorUtility.SetDirty(exParam);
            EditorUtility.SetDirty(exMenu);
            EditorUtility.SetDirty(poseMenu);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.ClearProgressBar();
        }


        public void SetupAnimatorLayer()
        {
            state_Wait = AvatarUtils.CreateAnimatorState(poseLayer, stateName_WAIT, defaultAnimationClip, false);
            state_Start = AvatarUtils.CreateAnimatorState(poseLayer, stateName_START, defaultAnimationClip, false);

            VRCPlayableLayerControl playableLayerControl = state_Start.AddStateMachineBehaviour<VRCPlayableLayerControl>();
            playableLayerControl.layer = (int)VRCAnimatorLayerControl.BlendableLayer.Action;
            playableLayerControl.goalWeight = 1.0f;
            playableLayerControl.blendDuration = 0.5f;

            VRCAnimatorTrackingControl animatorTrackingControl = state_Start.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            animatorTrackingControl.trackingHead = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingLeftHand = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingRightHand = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingHip = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingLeftFoot = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingRightFoot = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingLeftFingers = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingRightFingers = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Animation;
            animatorTrackingControl.trackingEyes = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange;
            animatorTrackingControl.trackingMouth = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange;

            VRCAnimatorLocomotionControl animatorLocomotionControl = state_Start.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
            animatorLocomotionControl.disableLocomotion = true;

            state_End = AvatarUtils.CreateAnimatorState(poseLayer, stateName_END, defaultAnimationClip, false);
            playableLayerControl = state_End.AddStateMachineBehaviour<VRCPlayableLayerControl>();
            playableLayerControl.layer = (int)VRCAnimatorLayerControl.BlendableLayer.Action;
            playableLayerControl.goalWeight = 0.0f;
            playableLayerControl.blendDuration = 0.25f;

            animatorTrackingControl = state_End.AddStateMachineBehaviour<VRCAnimatorTrackingControl>();
            animatorTrackingControl.trackingHead = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingLeftHand = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingRightHand = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingHip = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingLeftFoot = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingRightFoot = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingLeftFingers = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingRightFingers = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.Tracking;
            animatorTrackingControl.trackingEyes = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange;
            animatorTrackingControl.trackingMouth = VRC.SDKBase.VRC_AnimatorTrackingControl.TrackingType.NoChange;

            animatorLocomotionControl = state_End.AddStateMachineBehaviour<VRCAnimatorLocomotionControl>();
            animatorLocomotionControl.disableLocomotion = false;

            if (state_Wait.transitions.Length == 0)
            {
                AnimatorCondition add_animatorCondition = new AnimatorCondition();
                add_animatorCondition.parameter = parameterName;
                add_animatorCondition.mode = AnimatorConditionMode.NotEqual;
                add_animatorCondition.threshold = 0;
                AvatarUtils.CreateAnimatorTransion(state_Wait, state_Start, add_animatorCondition);

            }
            
            if(state_End.transitions.Length == 0)
            {
                state_End.AddExitTransition().AddCondition(AnimatorConditionMode.Equals, 0, parameterName);
            }

            for (int i = 0; i < VRCAnimatorStatelist.Count; i++)
            {
                for (int t = 0; t < VRCAnimatorStatelist[i].Count; t++)
                {
                    VRCAnimatorStatelist[i][t]._state = AvatarUtils.CreateAnimatorState(poseLayer, VRCAnimatorStatelist[i][t]._MenuName, VRCAnimatorStatelist[i][t]._animationClipValue, false);
                }
            }

            for (int i = 0; i < VRCAnimatorStatelist.Count; i++)
            {
                for (int t = 0; t < VRCAnimatorStatelist[i].Count; t++)
                {
                    AnimatorCondition add_animatorCondition = new AnimatorCondition();
                    add_animatorCondition.parameter = parameterName;
                    add_animatorCondition.mode = AnimatorConditionMode.Equals;
                    add_animatorCondition.threshold = i * 8 + t + 1;
                    AvatarUtils.CreateAnimatorTransion(state_Start, VRCAnimatorStatelist[i][t]._state, add_animatorCondition);

                    add_animatorCondition = new AnimatorCondition();
                    add_animatorCondition.parameter = parameterName;
                    add_animatorCondition.mode = AnimatorConditionMode.Equals;
                    add_animatorCondition.threshold = 0;
                    AvatarUtils.CreateAnimatorTransion(VRCAnimatorStatelist[i][t]._state, state_End, add_animatorCondition);

                    for (int m = 0; m < VRCAnimatorStatelist.Count; m++)
                    {
                        for (int n = 0; n < VRCAnimatorStatelist[m].Count; n++)
                        {
                            if (VRCAnimatorStatelist[i][t] != VRCAnimatorStatelist[m][n])
                            {
                                add_animatorCondition = new AnimatorCondition();
                                add_animatorCondition.parameter = parameterName;
                                add_animatorCondition.mode = AnimatorConditionMode.Equals;
                                add_animatorCondition.threshold = m * 8 + n + 1;
                                AvatarUtils.CreateAnimatorTransion(VRCAnimatorStatelist[i][t]._state, VRCAnimatorStatelist[m][n]._state, add_animatorCondition);
                            }
                        }
                    }
                }


            }
            
        }

        //・閲覧機能
        //専用レイヤー内のStateを一覧化
        private void SearchPoseStateList(GameObject gameObject)
        {
            UnityEditor.Animations.AnimatorController controller = AvatarUtils.GetAnimator(gameObject.GetComponent<VRCAvatarDescriptor>(), AvatarUtils.ACTIONLAYER);
            VRCAnimatorStatelist = new List<List<ScriptableObjectVRCPoseState>>();
            for(int i = 0; i < 8; i++)
            {
                VRCAnimatorStatelist.Add(new List<ScriptableObjectVRCPoseState>());
            }

            poseLayer = AvatarUtils.GetAnimatorLayer(controller, layerName);
            if (poseLayer != null)
            {

                foreach (var state in poseLayer.stateMachine.states)
                {
                    if (state.state.name == stateName_WAIT)
                    {
                        state_Wait = state.state;

                    }
                    else if (state.state.name == stateName_START)
                    {
                        state_Start = state.state;
                        for(int i = 0; i < state.state.transitions.Length; i++)
                        {
                            int listIndex = ((int)state.state.transitions[i].conditions[0].threshold - 1) / 8;
                            VRCAnimatorStatelist[listIndex].Add(CreateInstance<ScriptableObjectVRCPoseState>());
                            VRCAnimatorStatelist[listIndex][VRCAnimatorStatelist[listIndex].Count - 1]._MenuName = state.state.transitions[i].destinationState.name;
                            VRCAnimatorStatelist[listIndex][VRCAnimatorStatelist[listIndex].Count - 1]._layer = poseLayer;
                            VRCAnimatorStatelist[listIndex][VRCAnimatorStatelist[listIndex].Count - 1]._state = state.state.transitions[i].destinationState;
                            VRCAnimatorStatelist[listIndex][VRCAnimatorStatelist[listIndex].Count - 1]._animationClipValue = (AnimationClip)state.state.transitions[i].destinationState.motion;
                        }
                    }
                    else if(state.state.name == stateName_END)
                    {
                        state_End = state.state;

                    }
                    else
                    {

                    }
                }
            }
            InitializeReorderableList();
        }

        public void SetupPlayableGraph(AnimationClip clip)
        {
            playableGraph = UnityEngine.Playables.PlayableGraph.Create();
            var clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            var output = AnimationPlayableOutput.Create(playableGraph, "output", AvatarObject_Preview.gameObject.GetComponent<Animator>());
            output.SetSourcePlayable(clipPlayable);
        }
        public void PlayAnimation()
        {
            playableGraph.Play();
            Evaluate(0.0f);
        }

        public void StopAnimation()
        {
            playableGraph.Stop();
        }

        public void Evaluate(float time)
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Evaluate(time);
            }
        }

        public void DestroyPlayableGraph()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        private void InitializeIfNeeded()
        {
            //if (didInitialize)
            //{
            //    return;
            //}
            if (_previewScene != null)
            {
                _previewScene.Dispose();
            }

            playingAnimationClipName = "";
            _previewScene = new PreviewScene();
            AvatarObject_Preview = Instantiate(AvatarObject);
            AvatarObject_Preview.transform.position = new Vector3(0, 0, 0);
            AvatarObject_Preview.transform.rotation = new Quaternion(0,0,0,0);
            DestroyImmediate(AvatarObject_Preview.GetComponent<VRCAvatarDescriptor>());
            _previewScene.AddGameObject(AvatarObject_Preview);
            didInitialize = true;
        }
        

        private void OnDisable()
        {
            if (_previewScene != null)
            {
                _previewScene.Dispose();
            }
            didInitialize = false;
        }
        
        public Texture2D saveExMenuIcon(string folderPath, string fileName, RenderTexture renderTexture)
        {
            //保存フォルダ作成
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                AssetDatabase.Refresh();
            }
            //Texture保存
            var rt = renderTexture;
            var texture = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            var tmp = RenderTexture.active;
            RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            texture.Apply();
            RenderTexture.active = tmp;


            rt = RenderTexture.GetTemporary(256, 256);
            Graphics.Blit(texture, rt);

            var preRT = RenderTexture.active;
            RenderTexture.active = rt;
            var ret = new Texture2D(256, 256);
            ret.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            ret.Apply();
            RenderTexture.active = preRT;

            RenderTexture.ReleaseTemporary(rt);

            // 保存
            string filePath = folderPath + @"\" + fileName + ".png";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
            {
                AssetDatabase.DeleteAsset(filePath);
            }
            File.WriteAllBytes(filePath, ret.EncodeToPNG());
            Texture2D Icon = (Texture2D)AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));

            return Icon;
        }

        public Texture2D getExMenuIcon(string folderPath, string fileName, RenderTexture renderTexture)
        {
            // 保存
            string filePath = folderPath + @"\" + fileName + ".png";
            AssetDatabase.ImportAsset(filePath);

            var textureImporter = AssetImporter.GetAtPath(filePath) as TextureImporter;
            textureImporter.maxTextureSize = 256;
            Texture2D Icon = (Texture2D)AssetDatabase.LoadAssetAtPath(filePath, typeof(Texture2D));

            return Icon;
        }

        public Texture2D ReadPng(string path)
        {
            byte[] readBinary = ReadPngFile(path);

            int pos = 16; // 16バイトから開始

            int width = 0;
            for (int i = 0; i < 4; i++)
            {
                width = width * 256 + readBinary[pos++];
            }

            int height = 0;
            for (int i = 0; i < 4; i++)
            {
                height = height * 256 + readBinary[pos++];
            }

            Texture2D texture = new Texture2D(width, height);
            texture.LoadImage(readBinary);

            return texture;
        }

        public byte[] ReadPngFile(string path)
        {
            FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            BinaryReader bin = new BinaryReader(fileStream);
            byte[] values = bin.ReadBytes((int)bin.BaseStream.Length);

            bin.Close();

            return values;
        }

        //要素の描画
        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            //要素を書き換えられるようにフィールドを表示
            EditorGUILayout.BeginHorizontal();
            EditorGUI.LabelField(new Rect(rect.x, rect.y, 20, rect.height), (index + 1).ToString());
            
            string menuName = EditorGUI.TextField(new Rect(rect.x + 20, rect.y, rect.width * 4 / 10, rect.height), VRCAnimatorStatelist[_tabIndex][index]._MenuName);
            if (menuName != "" && menuName != VRCAnimatorStatelist[_tabIndex][index]._MenuName)
            {
                VRCAnimatorStatelist[_tabIndex][index]._MenuName = getNotSameName(menuName);
            }
            
            AnimationClip animationClip = (AnimationClip)EditorGUI.ObjectField(new Rect(rect.x + rect.width * 4 / 10 + 20, rect.y, rect.width * 3 / 10, rect.height), VRCAnimatorStatelist[_tabIndex][index]._animationClipValue, typeof(AnimationClip), true);
            if (animationClip != null && animationClip != VRCAnimatorStatelist[_tabIndex][index]._animationClipValue)
            {
                VRCAnimatorStatelist[_tabIndex][index]._animationClipValue = animationClip;
                menuName = animationClip.name;
                VRCAnimatorStatelist[_tabIndex][index]._MenuName = "";
                VRCAnimatorStatelist[_tabIndex][index]._MenuName = getNotSameName(menuName);
            }
            
            if (GUI.Button(new Rect(rect.x + rect.width * 7 / 10 + 20, rect.y, rect.width * 1 / 10, rect.height), "Play"))
            {
                if (VRCAnimatorStatelist[_tabIndex][index]._animationClipValue != null)
                {
                    playingAnimationClipName = VRCAnimatorStatelist[_tabIndex][index]._animationClipValue.name;
                    SetupPlayableGraph(VRCAnimatorStatelist[_tabIndex][index]._animationClipValue);
                    PlayAnimation();
                }
            }

            if (GUI.Button(new Rect(rect.x + rect.width * 8 / 10 + 20, rect.y, rect.width * 1 / 10, rect.height), "Move"))
            {
                GenericMenu menu = new GenericMenu();
                for (int i = 0; i < _tabToggles.Count; i++)
                {
                    if (i == _tabIndex) continue;
                    menu.AddItem(new GUIContent(_tabToggles[i] + "に移動"), false, (object obj) => {
                        int moveIndex = (int)obj;
                        if (VRCAnimatorStatelist[moveIndex].Count > 7)
                        {
                            if (EditorUtility.DisplayDialog("Error", "Menu" + (moveIndex + 1) + "に空きがないため移動できません。", "OK")) ;
                        }
                        else
                        {
                            VRCAnimatorStatelist[moveIndex].Add(CreateInstance<ScriptableObjectVRCPoseState>());
                            VRCAnimatorStatelist[moveIndex][VRCAnimatorStatelist[moveIndex].Count - 1]._MenuName = VRCAnimatorStatelist[_tabIndex][index]._MenuName;
                            VRCAnimatorStatelist[moveIndex][VRCAnimatorStatelist[moveIndex].Count - 1]._animationClipValue = VRCAnimatorStatelist[_tabIndex][index]._animationClipValue;
                            VRCAnimatorStatelist[_tabIndex].Remove(VRCAnimatorStatelist[_tabIndex][index]);
                        }
                    }, i);
                }
                menu.ShowAsContext();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void Add(UnityEditorInternal.ReorderableList list)
        {
            list.list.Add(CreateInstance<ScriptableObjectVRCPoseState>());
            VRCAnimatorStatelist[_tabIndex][VRCAnimatorStatelist[_tabIndex].Count - 1]._MenuName = getNotSameName(defaultStateName);
        }

        private bool CanAdd(UnityEditorInternal.ReorderableList list)
        {
            return VRCAnimatorStatelist[_tabIndex].Count < 8;//8個以下しか追加できないように
        }

        private void DrawHeader(Rect rect)
        {
            EditorGUI.LabelField(rect, "アニメーション一覧");
        }

        public void OnDrawElementBackground(Rect rect, int index, bool isActive, bool isFocused)
        {
            Texture2D tex = new Texture2D(1, 1);
            if (isFocused)
            {
                tex.SetPixel(0, 0, new Color(0f, 0.5f, 1f, 0.4f));
            }
            else if(index % 2 == 1)
            {
                tex.SetPixel(0, 0, new Color(0.25f, 0.25f, 0.25f, 1f));
            }
            else
            {
                tex.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1f));
            }
            tex.Apply();
            GUI.DrawTexture(rect, tex as Texture);
        }

        private bool checkSameMenuName(string menuName)
        {
            bool checkSameMenuName = false;
            for (int i = 0; i < VRCAnimatorStatelist.Count; i++)
            {
                for(int t = 0; t< VRCAnimatorStatelist[i].Count; t++)
                {
                    if (VRCAnimatorStatelist[i][t]._MenuName == menuName) checkSameMenuName = true;
                }
            }
            return checkSameMenuName;
        }

        private string getNotSameName(string menuName)
        {
            int i = 0;
            string getNotSameName;
            
            if (!checkSameMenuName(menuName)) return menuName;

            bool flg = false;
            do{
                getNotSameName = menuName + " "+ i.ToString();
                flg = checkSameMenuName(getNotSameName);
                i += 1;
            }
            while (flg) ;

            return getNotSameName;
        }

        private void InitializeReorderableList()
        {
            VRCAnimatorState_ReorderableList = new UnityEditorInternal.ReorderableList(VRCAnimatorStatelist[_tabIndex], typeof(ScriptableObjectVRCPoseState), true, false, true, true);
            VRCAnimatorState_ReorderableList.draggable = true;
            VRCAnimatorState_ReorderableList.drawElementCallback += DrawElement;
            VRCAnimatorState_ReorderableList.onAddCallback += Add;
            VRCAnimatorState_ReorderableList.onCanAddCallback += CanAdd;
            VRCAnimatorState_ReorderableList.drawHeaderCallback += DrawHeader;
            VRCAnimatorState_ReorderableList.drawElementBackgroundCallback += OnDrawElementBackground;
        }


    }

}
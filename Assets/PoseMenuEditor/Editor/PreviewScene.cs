using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using UnityEngine.Rendering;
using System.Linq;
using System.IO;

namespace HakuroEditor.PoseMenuEditor.Preview
{
    
public class PreviewScene : IDisposable
{
    public Scene Scene { get; private set; }
    public Camera Camera { get; private set; }
    public RenderTexture RenderTexture { get; private set; }
    public Vector2Int RenderTextureSize { get; set; } = new Vector2Int(1024, 1024);

    private SavedRenderSettings _savedRenderSettings;
    private List<GameObject> _gameObjects = new List<GameObject>();
    private bool _didInitialize = false;

    public PreviewScene(string environmentSceneAssetPath = null)
    {
        try
        {
            Scene = EditorSceneManager.NewPreviewScene();

            if (environmentSceneAssetPath != null)
            {
                _savedRenderSettings = SavedRenderSettings.Create(environmentSceneAssetPath);
                CopyRootGameObjects(environmentSceneAssetPath);
            }

            // Deactivate unused cameras
            var oldCameras = Scene
                .GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<Camera>());
            foreach (var oldCamera in oldCameras)
            {
                oldCamera.enabled = false;
            }

            var cameraGO = new GameObject("Preview Scene Camera", typeof(Camera));
                AddGameObject(cameraGO);
            Camera = cameraGO.GetComponent<Camera>();
            Camera.cameraType = CameraType.Preview;
            Camera.forceIntoRenderTexture = true;
            Camera.scene = Scene;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.enabled = false; // Deactivate so as not to affect GameView

            var hasDirectionalLight = Scene
                .GetRootGameObjects()
                .SelectMany(x => x.GetComponentsInChildren<Light>())
                .Any(x => x.type == LightType.Directional);
            if (!hasDirectionalLight)
            {
                var lightGO = new GameObject("Directional Light", typeof(Light));
                AddGameObject(lightGO);
                lightGO.transform.rotation = Quaternion.Euler(50, -30, 0);
                var light = lightGO.GetComponent<Light>();
                light.type = LightType.Directional;
            }

            _didInitialize = true;
        }
        catch (Exception e)
        {
            Dispose();
            _didInitialize = false;
            throw e;
        }
    }

    public void Render(bool useScriptableRenderPipeline = false)
    {
        if (!_didInitialize)
        {
            return;
        }
        // Change RenderSettings
        if (_savedRenderSettings != null && Unsupported.SetOverrideLightingSettings(Scene))
        {
            _savedRenderSettings.Apply();
        }

        // Create RenderTexture if needed
        if (!RenderTexture || RenderTexture.width != RenderTextureSize.x || RenderTexture.height != RenderTextureSize.y)
        {
            if (RenderTexture)
            {
                Object.DestroyImmediate(RenderTexture);
                RenderTexture = null;
            }

            var format = Camera.allowHDR ? GraphicsFormat.R16G16B16A16_SFloat : GraphicsFormat.R8G8B8A8_UNorm;
            RenderTexture = new RenderTexture(RenderTextureSize.x, RenderTextureSize.y, 32, format);
        }
        Camera.targetTexture = RenderTexture;

        // Render
        var oldAllowPipes = Unsupported.useScriptableRenderPipeline;
        Unsupported.useScriptableRenderPipeline = useScriptableRenderPipeline;
        Camera.Render();
        Unsupported.useScriptableRenderPipeline = oldAllowPipes;

        Camera.targetTexture = null;
        // Restore RenderSettings
        if (_savedRenderSettings != null)
        {
            Unsupported.RestoreOverrideLightingSettings();
        }
    }

    public void Dispose()
    {
        Camera.targetTexture = null;

        if (RenderTexture != null)
        {
            Object.DestroyImmediate(RenderTexture);
            RenderTexture = null;
        }

        foreach (var go in _gameObjects)
        {
            Object.DestroyImmediate(go);
        }
        _gameObjects.Clear();

        EditorSceneManager.ClosePreviewScene(Scene);
    }

    /// <summary>
    /// Add GameObject to preview scene
    /// </summary>
    public void AddGameObject(GameObject go)
    {
        if (_gameObjects.Contains(go))
        {
            return;
        }
        SceneManager.MoveGameObjectToScene(go, Scene);
        _gameObjects.Add(go);
    }

    /// <summary>
    /// Add prefab instance to preview scene
    /// </summary>
    public GameObject InstantiatePrefab(GameObject prefab)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Scene);
        _gameObjects.Add(instance);
        return instance;
    }

    /// <summary>
    /// Copy all root GameObjects of source scene to preview scene
    /// </summary>
    private void CopyRootGameObjects(string sourceSceneAssetPath)
    {
        GameObject[] rootGameObjects = null;
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (scene.path == sourceSceneAssetPath)
            {
                rootGameObjects = scene.GetRootGameObjects();
                break;
            }
        }
        if (rootGameObjects == null)
        {
            var scene = EditorSceneManager.OpenScene(sourceSceneAssetPath, OpenSceneMode.Additive);
            rootGameObjects = scene.GetRootGameObjects();
            EditorSceneManager.CloseScene(scene, true);
        }
        if (rootGameObjects != null)
        {
            foreach (var rootGameObject in rootGameObjects)
            {
                AddGameObject(GameObject.Instantiate(rootGameObject));
            }
        }
    }

    public class SavedRenderSettings
    {
        private Material _skybox;
        private Light _sun;
        private AmbientMode _ambientMode;
        private SphericalHarmonicsL2 _ambientProbe;
        private Color _ambientSkyColor;
        private Color _ambientEquatorColor;
        private Color _ambientGroundColor;
        private Color _ambientLight;
        private float _ambientIntensity;
        private DefaultReflectionMode _defaultReflectionMode;
        private int _defaultReflectionResolution;
        private Cubemap _customReflection;
        private float _reflectionIntensity;
        private int _reflectionBounces;
        private Color _substractiveShadowColor;
        private bool _fog;
        private FogMode _fogMode;
        private Color _fogColor;
        private float _fogDensity;
        private float _fogStartDistance;
        private float _fogEndDistance;

        public static SavedRenderSettings Create(string scenePath)
        {
            var result = new SavedRenderSettings();
            Scene? oldActiveScene = null;
            if (scenePath != EditorSceneManager.GetActiveScene().path)
            {
                oldActiveScene = EditorSceneManager.GetActiveScene();
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
                EditorSceneManager.SetActiveScene(scene);
            }

            result._skybox = RenderSettings.skybox;
            result._sun = RenderSettings.sun;
            result._ambientMode = RenderSettings.ambientMode;
            result._ambientProbe = RenderSettings.ambientProbe;
            result._ambientSkyColor = RenderSettings.ambientSkyColor;
            result._ambientEquatorColor = RenderSettings.ambientEquatorColor;
            result._ambientGroundColor = RenderSettings.ambientGroundColor;
            result._ambientLight = RenderSettings.ambientLight;
            result._ambientIntensity = RenderSettings.ambientIntensity;
            result._defaultReflectionMode = RenderSettings.defaultReflectionMode;
            result._defaultReflectionResolution = RenderSettings.defaultReflectionResolution;
            result._customReflection = RenderSettings.customReflection;
            // If defaultReflectionMode is Skybox, search and set the created cube map
            if (result._defaultReflectionMode == DefaultReflectionMode.Skybox && Lightmapping.lightingDataAsset != null)
            {
                var lightingDataAssetPath = AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset);
                var lightingDataAssetDirectoryName = Path.GetDirectoryName(lightingDataAssetPath);
                var environmentProbeAssetPath = Directory
                    .GetFiles(lightingDataAssetDirectoryName)
                    .FirstOrDefault(x => x.EndsWith(".exr"));
                if (!string.IsNullOrEmpty(environmentProbeAssetPath))
                {
                    result._defaultReflectionMode = DefaultReflectionMode.Custom;
                    result._customReflection = AssetDatabase.LoadAssetAtPath<Cubemap>(environmentProbeAssetPath.Replace("\\", "/"));
                }
            }
            result._reflectionIntensity = RenderSettings.reflectionIntensity;
            result._reflectionBounces = RenderSettings.reflectionBounces;
            result._substractiveShadowColor = RenderSettings.subtractiveShadowColor;
            result._fog = RenderSettings.fog;
            result._fogMode = RenderSettings.fogMode;
            result._fogColor = RenderSettings.fogColor;
            result._fogDensity = RenderSettings.fogDensity;
            result._fogStartDistance = RenderSettings.fogStartDistance;
            result._fogEndDistance = RenderSettings.fogEndDistance;

            if (oldActiveScene.HasValue)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.SetActiveScene(oldActiveScene.Value);
                EditorSceneManager.CloseScene(scene, true);
            }
            return result;
        }

        public void Apply()
        {
            RenderSettings.skybox = _skybox;
            RenderSettings.sun = _sun;
            RenderSettings.ambientMode = _ambientMode;
            RenderSettings.ambientProbe = _ambientProbe;
            RenderSettings.ambientSkyColor = _ambientSkyColor;
            RenderSettings.ambientEquatorColor = _ambientEquatorColor;
            RenderSettings.ambientGroundColor = _ambientGroundColor;
            RenderSettings.ambientLight = _ambientLight;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.defaultReflectionMode = _defaultReflectionMode;
            RenderSettings.defaultReflectionResolution = _defaultReflectionResolution;
            RenderSettings.customReflection = _customReflection;
            RenderSettings.reflectionIntensity = _reflectionIntensity;
            RenderSettings.reflectionBounces = _reflectionBounces;
            RenderSettings.subtractiveShadowColor = _substractiveShadowColor;
            RenderSettings.fog = _fog;
            RenderSettings.fogMode = _fogMode;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogDensity = _fogDensity;
            RenderSettings.fogStartDistance = _fogStartDistance;
            RenderSettings.fogEndDistance = _fogEndDistance;
        }
    }
}
}
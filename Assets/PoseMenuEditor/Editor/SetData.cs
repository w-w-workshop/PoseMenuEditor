using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;

namespace HakuroEditor.PoseMenuEditor.ObjectData
{
    [Serializable]
    [CreateAssetMenu(menuName = "ScriptableObject/SetData")]
    public class SetData : ScriptableObject
    {
        public Vector3 cameraPosition = new Vector3(0, 1, 2);
        public Quaternion cameraRotation = new Quaternion(0, 180, 0, 0);
        public float cameraFieldOfView = 60f;
    }    

    [Serializable]
    public class ScriptableObjectVRCPoseState : ScriptableObject
    {
        [SerializeField]
        public AnimatorStateTransition _transition;
        [SerializeField]
        public AnimationClip _animationClipValue;
        [SerializeField]
        public GameObject _gameObjectValue;
        [SerializeField]
        public string _MenuName;
        [SerializeField]
        public string _objectNameValue;
        [SerializeField]
        public AnimatorControllerLayer _layer;
        [SerializeField]
        public AnimatorState _state;
    }

}
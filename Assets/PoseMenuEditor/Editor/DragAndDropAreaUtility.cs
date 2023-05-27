//  DragAndDropAreaUtility.cs
//  http://kan-kikuchi.hatenablog.com/entry/DragAndDropAreaUtility
//
//  Created by kan.kikuchi on 2021.01.17.

using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace HakuroEditor.PoseMenuEditor
{
    /// �h���b�N&�h���b�v�ŃI�u�W�F�N�g���擾����GUI��\������N���X
    public static class DragAndDropAreaUtility
    {
        
        /// �h���b�N&�h���b�v�ŃI�u�W�F�N�g���擾����GUI�\��(�擾���ĂȂ�����null���Ԃ�)
        public static T GetObject<T>(string areaTitle = "Drag & Drop", float widthMin = 0, float height = 50) where T : Object
        {
            //�h���b�N�h���b�v���ꂽ�I�u�W�F�N�g�擾
            var objectReferences = GetObjects(areaTitle, widthMin, height);

            //�h���b�v���ꂽ�I�u�W�F�N�g�ɑΏۂ̕�������ΕԂ�
            return objectReferences?.FirstOrDefault(o => o is T) as T;
        }

        /// �h���b�N&�h���b�v�ŕ����̃I�u�W�F�N�g���擾����GUI�\��(�擾����������true���Ԃ�A�擾��������targetList��add�����)
        public static bool GetObjects<T>(List<T> targetList, string areaTitle = "Drag & Drop", float widthMin = 0, float height = 50) where T : Object
        {
            //�h���b�N�h���b�v���ꂽ�I�u�W�F�N�g���Ȃ���΃X���[
            var objectReferences = GetObjects(areaTitle, widthMin, height);
            if (objectReferences == null)
            {
                return false;
            }

            //�h���b�v���ꂽ�I�u�W�F�N�g�ɑΏۂ̕���������΃X���[
            var targetObjectReferences = objectReferences.OfType<T>().ToList();
            if (targetObjectReferences.Count == 0)
            {
                return false;
            }

            //�Ώۂ�o�^
            targetList.AddRange(targetObjectReferences);
            return true;
        }

        //�h���b�N�h���b�v�ŕ����̃I�u�W�F�N�g���擾����GUI�\��(�擾���ĂȂ�����null���Ԃ�)
        private static Object[] GetObjects(string areaTitle = "Drag & Drop", float widthMin = 0, float height = 50)
        {
            //D&D�o����ꏊ��`��
            var dropArea = GUILayoutUtility.GetRect(widthMin, height, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, areaTitle);

            //�}�E�X�̈ʒu��D&D�͈̔͂ɂȂ���΃X���[
            if (!dropArea.Contains(Event.current.mousePosition))
            {
                return null;
            }

            //���݂̃C�x���g���擾
            var eventType = Event.current.type;

            //�h���b�O���h���b�v�ő��삪�X�V���ꂽ���ł��A���s�������ł��Ȃ���΃X���[
            if (eventType != EventType.DragUpdated && eventType != EventType.DragPerform)
            {
                return null;
            }

            //�J�[�\����+�̃A�C�R����\��
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            //�h���b�O���h���b�v�Ŗ�����΃X���[
            if (eventType != EventType.DragPerform)
            {
                return null;
            }

            //�h���b�O���󂯕t����(�h���b�O���ăJ�[�\���ɂ����t���Ă��I�u�W�F�N�g���߂�Ȃ��Ȃ�̂�)
            DragAndDrop.AcceptDrag();

            //�C�x���g���g�p�ς݂ɂ���
            Event.current.Use();

            return DragAndDrop.objectReferences;
        }

    }
}
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public class AdvancedCloneTool
{
    private enum ToolState { Init, Ready, Cloning }
    private static ToolState _currentState = ToolState.Init;

    private struct TransformDelta
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        public TransformDelta(Transform t)
        {
            position = t.position;
            rotation = t.rotation;
            scale = t.localScale;
        }

        public static TransformDelta operator -(TransformDelta a, TransformDelta b) => new TransformDelta
        {
            position = a.position - b.position,
            rotation = a.rotation * Quaternion.Inverse(b.rotation),
            scale = a.scale - b.scale
        };

        public static TransformDelta operator +(TransformDelta a, TransformDelta b) => new TransformDelta
        {
            position = a.position + b.position,
            rotation = a.rotation * b.rotation,
            scale = a.scale + b.scale
        };

        public void ApplyTo(Transform target)
        {
            target.position = position;
            target.rotation = rotation;
            target.localScale = scale;
        }
    }

    private static GameObject _baseObject;
    private static TransformDelta _baseData;
    private static TransformDelta _currentDelta;
    private static int _cloneCounter;
    private static List<GameObject> _trackedObjects = new List<GameObject>();

    [MenuItem("CC美术友好小工具/等间距复制  Ctrl+Shift+D")]
    static void Execute()
    {
        var selected = Selection.activeGameObject;
        if (!selected)
        {
            Debug.Log("请选择物体");
            return;
        }

        // 新物体检测
        if (_currentState != ToolState.Init && !IsClone(selected) && selected != _baseObject)
        {
            Debug.Log("检测到新物体，重置工具");
            ResetTool();
            return;
        }

        switch (_currentState)
        {
            case ToolState.Init:
                InitializeProcess(selected);
                break;

            case ToolState.Ready:
                CalculateDelta(selected);
                CreateClone(selected);
                _currentState = ToolState.Cloning;
                break;

            case ToolState.Cloning:
                CreateClone(selected);
                break;
        }
    }

    #region 核心功能
    static void InitializeProcess(GameObject original)
    {
        _baseObject = original;
        _baseData = new TransformDelta(original.transform);
        _cloneCounter = 1;

        var firstClone = CreateClone(original, 1);
        TrackObject(firstClone);
        TrackObject(original);

        Selection.activeGameObject = firstClone;
        _currentState = ToolState.Ready;
        Debug.Log($"初始化完成: {original.name}");
    }

    static void CalculateDelta(GameObject modified)
    {
        var modifiedData = new TransformDelta(modified.transform);
        _currentDelta = modifiedData - _baseData;
        Debug.Log($"位置差: {_currentDelta.position}");
    }

    static void CreateClone(GameObject current)
    {
        var newClone = CreateClone(current, ++_cloneCounter);
        var newData = new TransformDelta(current.transform) + _currentDelta;
        newData.ApplyTo(newClone.transform);

        TrackObject(newClone);
        Selection.activeGameObject = newClone;
        Debug.Log($"生成克隆体: {newClone.name}");
    }

    static GameObject CreateClone(GameObject original, int index)
    {
        var clone = Object.Instantiate(original);
        clone.name = $"{_baseObject.name}_Clone_{index:D2}";
        Undo.RegisterCreatedObjectUndo(clone, "Advanced Clone");
        return clone;
    }
    #endregion

    #region 状态管理
    static bool IsClone(GameObject obj)
    {
        return _trackedObjects.Contains(obj);
    }

    static void TrackObject(GameObject obj)
    {
        if (!_trackedObjects.Contains(obj))
            _trackedObjects.Add(obj);
    }

    static void ResetTool()
    {
        _trackedObjects.Clear();
        _currentState = ToolState.Init;
        _baseObject = null;
        _cloneCounter = 0;
        Debug.Log("工具已重置");
    }

    [InitializeOnLoad]
    class StateWatcher
    {
        static StateWatcher()
        {
            EditorApplication.hierarchyChanged += () =>
            {
                // 自动清理已销毁的物体
                _trackedObjects.RemoveAll(obj => obj == null);
            };

            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode)
                    ResetTool();
            };
        }
    }
    #endregion
}
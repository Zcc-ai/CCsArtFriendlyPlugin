using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class SetPivotToBottom : EditorWindow
{
    [MenuItem("CC美术友好小工具/轴心点变为底面中心")]
    static void Init()
    {
        GetWindow<SetPivotToBottom>("Pivot Tool").Show();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Set Selected Object's Pivot to Bottom"))
        {
            SetPivot();
        }
    }

    static void SetPivot()
    {
        foreach (GameObject selected in Selection.gameObjects)
        {
            Vector3 bottomCenter = CalculateBottomCenter(selected);
            CreateNewParent(selected, bottomCenter);
        }
    }

    static Vector3 CalculateBottomCenter(GameObject obj)
    {
        List<Vector3> allVertices = new List<Vector3>();

        // 收集所有Mesh顶点
        foreach (MeshFilter mf in obj.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;

            foreach (Vector3 vertex in mf.sharedMesh.vertices)
            {
                Vector3 worldPos = mf.transform.TransformPoint(vertex);
                allVertices.Add(worldPos);
            }
        }

        // 收集所有SkinnedMesh顶点
        foreach (SkinnedMeshRenderer smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (smr.sharedMesh == null) continue;

            foreach (Vector3 vertex in smr.sharedMesh.vertices)
            {
                Vector3 worldPos = smr.transform.TransformPoint(vertex);
                allVertices.Add(worldPos);
            }
        }

        if (allVertices.Count == 0)
        {
            Debug.LogWarning("No vertices found: " + obj.name);
            return obj.transform.position;
        }

        // 计算最低点
        float minY = allVertices[0].y;
        foreach (Vector3 v in allVertices)
        {
            if (v.y < minY) minY = v.y;
        }

        // 计算底部中心
        float sumX = 0, sumZ = 0;
        int count = 0;
        foreach (Vector3 v in allVertices)
        {
            if (Mathf.Approximately(v.y, minY))
            {
                sumX += v.x;
                sumZ += v.z;
                count++;
            }
        }

        return count > 0 ?
            new Vector3(sumX / count, minY, sumZ / count) :
            obj.transform.position;
    }

    static void CreateNewParent(GameObject original, Vector3 pivotPosition)
    {
        Undo.SetCurrentGroupName("Set Pivot to Bottom");

        // 创建新父物体
        GameObject newParent = new GameObject(original.name + "_Pivot");
        Undo.RegisterCreatedObjectUndo(newParent, "Create Pivot Parent");

        // 设置父物体位置
        newParent.transform.position = pivotPosition;

        // 记录原始变换
        Vector3 originalPosition = original.transform.position;
        Quaternion originalRotation = original.transform.rotation;
        Vector3 originalScale = original.transform.localScale;
        Transform originalParent = original.transform.parent;

        // 设置父级关系
        Undo.SetTransformParent(original.transform, newParent.transform, "Reparent Object");

        // 保持原始变换
        original.transform.position = originalPosition;
        original.transform.rotation = originalRotation;
        original.transform.localScale = originalScale;

        // 保持层级关系
        if (originalParent != null)
        {
            newParent.transform.SetParent(originalParent, true);
        }

        // 选中新父物体
        Selection.activeGameObject = newParent;
    }
}
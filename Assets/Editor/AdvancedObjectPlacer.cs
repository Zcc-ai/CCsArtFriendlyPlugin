using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class AdvancedObjectPlacer : EditorWindow
{
    // 配置参数
    private GameObject targetObject;
    private List<GameObject> objectPrefabs = new List<GameObject>();
    private int spawnCount = 10;
    private Vector2 scaleRange = new Vector2(0.5f, 2f);
    private Vector3 minRotation = Vector3.zero;
    private Vector3 maxRotation = new Vector3(360, 360, 360);
    private bool useSurfaceNormal = true;
    private bool uniformScaling = true;
    private bool isClosedShape = true;
    private bool generateOnEdges = false;
    private bool evenSpacing = false;
    private Vector2 scrollPos;

    [MenuItem("CC美术友好小工具/高级物体生成器")]
    public static void ShowWindow()
    {
        GetWindow<AdvancedObjectPlacer>("物体生成器");
    }

    void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 基础设置
        GUILayout.Label("基础设置", EditorStyles.boldLabel);
        targetObject = (GameObject)EditorGUILayout.ObjectField("目标物体", targetObject, typeof(GameObject), true);

        // 形状设置
        GUILayout.Space(10);
        GUILayout.Label("形状设置", EditorStyles.boldLabel);
        isClosedShape = EditorGUILayout.Toggle("是闭合物体", isClosedShape);

        if (!isClosedShape)
        {
            EditorGUI.indentLevel++;
            generateOnEdges = EditorGUILayout.Toggle("沿边缘生成", generateOnEdges);
            if (generateOnEdges)
            {
                evenSpacing = EditorGUILayout.Toggle("等距生成", evenSpacing);
            }
            EditorGUI.indentLevel--;
        }

        // 生成设置
        GUILayout.Space(10);
        GUILayout.Label("生成设置", EditorStyles.boldLabel);
        spawnCount = EditorGUILayout.IntField("生成数量", spawnCount);

        // 预制体列表
        GUILayout.Space(10);
        GUILayout.Label("物体预制体", EditorStyles.boldLabel);
        for (int i = 0; i < objectPrefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            objectPrefabs[i] = (GameObject)EditorGUILayout.ObjectField($"预制体 {i + 1}", objectPrefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                objectPrefabs.RemoveAt(i);
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("添加新预制体")) objectPrefabs.Add(null);

        // 随机范围
        GUILayout.Space(10);
        GUILayout.Label("随机范围", EditorStyles.boldLabel);
        scaleRange = EditorGUILayout.Vector2Field("缩放范围", scaleRange);
        uniformScaling = EditorGUILayout.Toggle("统一缩放", uniformScaling);
        minRotation = EditorGUILayout.Vector3Field("最小旋转", minRotation);
        maxRotation = EditorGUILayout.Vector3Field("最大旋转", maxRotation);
        useSurfaceNormal = EditorGUILayout.Toggle("对齐表面", useSurfaceNormal);

        // 生成按钮
        GUILayout.Space(20);
        if (GUILayout.Button("生成物体", GUILayout.Height(40)))
        {
            GenerateObjects();
        }

        EditorGUILayout.EndScrollView();
    }

    void GenerateObjects()
    {
        if (!ValidateSettings()) return;

        Collider collider = targetObject.GetComponent<Collider>();
        if (collider == null) return;

        if (!isClosedShape && generateOnEdges)
        {
            GenerateAlongEdges(collider);
        }
        else
        {
            GenerateOnSurface(collider);
        }
    }

    // 验证设置
    bool ValidateSettings()
    {
        if (targetObject == null)
        {
            Debug.LogError("请指定目标物体！");
            return false;
        }

        if (targetObject.GetComponent<Collider>() == null)
        {
            Debug.LogError("目标物体需要碰撞体！");
            return false;
        }

        if (objectPrefabs.Count == 0 || objectPrefabs.All(x => x == null))
        {
            Debug.LogError("请至少添加一个有效预制体！");
            return false;
        }

        return true;
    }

    // 边缘生成逻辑
    void GenerateAlongEdges(Collider collider)
    {
        List<Vector3> edgePoints = GetEdgePoints(collider);
        if (edgePoints.Count == 0) return;

        if (evenSpacing)
        {
            float totalLength = CalculateEdgeLength(edgePoints);
            float spacing = totalLength / spawnCount;

            for (int i = 0; i < spawnCount; i++)
            {
                float targetDistance = i * spacing;
                Vector3 position = GetPositionAtDistance(edgePoints, targetDistance);
                CreateEdgeInstance(position);
            }
        }
        else
        {
            for (int i = 0; i < spawnCount; i++)
            {
                Vector3 position = GetRandomEdgePosition(edgePoints);
                CreateEdgeInstance(position);
            }
        }
    }

    // 表面生成逻辑
    void GenerateOnSurface(Collider collider)
    {
        for (int i = 0; i < spawnCount; i++)
        {
            if (FindValidPosition(collider, out Vector3 position, out Vector3 normal))
            {
                GameObject prefab = GetValidPrefab();
                if (prefab == null) continue;

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Create Object");

                instance.transform.SetParent(targetObject.transform, true);
                instance.transform.position = position;
                instance.transform.rotation = GetSurfaceRotation(normal);
                instance.transform.localScale = GetRandomScale();
                instance.transform.Rotate(GetRandomEuler(), Space.Self);
            }
        }
    }

    // 核心工具方法
    GameObject GetValidPrefab()
    {
        return objectPrefabs
            .Where(p => p != null)
            .OrderBy(_ => Random.value)
            .FirstOrDefault();
    }

    Vector3 GetRandomEuler()
    {
        return new Vector3(
            Random.Range(minRotation.x, maxRotation.x),
            Random.Range(minRotation.y, maxRotation.y),
            Random.Range(minRotation.z, maxRotation.z)
        );
    }

    Vector3 GetRandomScale()
    {
        if (uniformScaling)
        {
            float scale = Random.Range(scaleRange.x, scaleRange.y);
            return new Vector3(scale, scale, scale);
        }
        return new Vector3(
            Random.Range(scaleRange.x, scaleRange.y),
            Random.Range(scaleRange.x, scaleRange.y),
            Random.Range(scaleRange.x, scaleRange.y)
        );
    }

    bool FindValidPosition(Collider collider, out Vector3 position, out Vector3 normal)
    {
        position = Vector3.zero;
        normal = Vector3.up;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(collider.bounds.min.x, collider.bounds.max.x),
                collider.bounds.center.y,
                Random.Range(collider.bounds.min.z, collider.bounds.max.z)
            );

            Ray ray = new Ray(
                randomPoint + Vector3.up * collider.bounds.size.y * 2,
                Vector3.down
            );

            if (collider.Raycast(ray, out RaycastHit hit, collider.bounds.size.y * 3))
            {
                position = hit.point;
                normal = hit.normal;
                return true;
            }
        }
        return false;
    }

    Quaternion GetSurfaceRotation(Vector3 normal)
    {
        return useSurfaceNormal ?
            Quaternion.FromToRotation(Vector3.up, normal) :
            Quaternion.identity;
    }

    // 边缘生成辅助方法
    void CreateEdgeInstance(Vector3 position)
    {
        GameObject prefab = GetValidPrefab();
        if (prefab == null) return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Create Edge Object");

        instance.transform.SetParent(targetObject.transform, true);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(GetRandomEuler());
        instance.transform.localScale = GetRandomScale();
    }

    List<Vector3> GetEdgePoints(Collider collider)
    {
        List<Vector3> points = new List<Vector3>();
        Bounds bounds = collider.bounds;

        if (bounds.size.y < 0.1f)
        {
            float xMin = bounds.min.x;
            float xMax = bounds.max.x;
            float zMin = bounds.min.z;
            float zMax = bounds.max.z;
            float y = bounds.center.y;

            points.AddRange(GenerateEdgePath(new Vector3(xMin, y, zMin), new Vector3(xMax, y, zMin), 10));
            points.AddRange(GenerateEdgePath(new Vector3(xMax, y, zMin), new Vector3(xMax, y, zMax), 10));
            points.AddRange(GenerateEdgePath(new Vector3(xMax, y, zMax), new Vector3(xMin, y, zMax), 10));
            points.AddRange(GenerateEdgePath(new Vector3(xMin, y, zMax), new Vector3(xMin, y, zMin), 10));
        }

        return points;
    }

    IEnumerable<Vector3> GenerateEdgePath(Vector3 start, Vector3 end, int segments)
    {
        for (int i = 0; i <= segments; i++)
        {
            yield return Vector3.Lerp(start, end, (float)i / segments);
        }
    }

    float CalculateEdgeLength(List<Vector3> points)
    {
        float length = 0;
        for (int i = 1; i < points.Count; i++)
        {
            length += Vector3.Distance(points[i - 1], points[i]);
        }
        return length;
    }

    Vector3 GetPositionAtDistance(List<Vector3> path, float targetDistance)
    {
        float currentDistance = 0;
        for (int i = 1; i < path.Count; i++)
        {
            float segmentLength = Vector3.Distance(path[i - 1], path[i]);
            if (currentDistance + segmentLength >= targetDistance)
            {
                float t = (targetDistance - currentDistance) / segmentLength;
                return Vector3.Lerp(path[i - 1], path[i], t);
            }
            currentDistance += segmentLength;
        }
        return path.Last();
    }

    Vector3 GetRandomEdgePosition(List<Vector3> edgePoints)
    {
        int index = Random.Range(0, edgePoints.Count - 1);
        float t = Random.Range(0f, 1f);
        return Vector3.Lerp(edgePoints[index], edgePoints[index + 1], t);
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

public class VoronoiMeshGenerator : MonoBehaviour
{
    public float mapWidth = 50f;
    public float mapHeight = 50f;
    public int pointCount = 30;
    public Material cellMaterial; // Перетащите сюда любой материал в инспекторе

    void Start()
    {
        // 1. Генерируем случайные входные точки
        List<VoronoiGeometry.Vector2D> sites = new List<VoronoiGeometry.Vector2D>();
        for (int i = 0; i < pointCount; i++)
        {
            sites.Add(new VoronoiGeometry.Vector2D(UnityEngine.Random.Range(2f, mapWidth - 2f), UnityEngine.Random.Range(2f, mapHeight - 2f)));
        }

        // 2. Рассчитываем геометрию Вороного через наш чистый C# класс
        List<VoronoiGeometry.Cell> cells = VoronoiGeometry.BowyerWatsonVoronoi(sites, mapWidth, mapHeight);

        // 3. Строим меш для каждой ячейки
        foreach (var cell in cells)
        {
            if (cell.Vertices.Count < 3) continue;

            CreateCellGameObject(cell);
        }
    }

    void CreateCellGameObject(VoronoiGeometry.Cell cell)
    {
        GameObject cellObj = new GameObject("Voronoi_Cell_" + cell.Site.GetHashCode());
        cellObj.transform.parent = this.transform;

        MeshFilter meshFilter = cellObj.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = cellObj.AddComponent<MeshRenderer>();
        meshRenderer.material = cellMaterial;

        // Даем ячейке случайный цвет (для наглядности границ)
        meshRenderer.material.color = new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);

        Mesh mesh = new Mesh();

        // Формируем массивы вершин и треугольников для Unity Mesh
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        // Центральная точка полигона (индекс 0)
        vertices.Add(new Vector3(cell.Site.X, 0, cell.Site.Y));

        // Граничные точки
        for (int i = 0; i < cell.Vertices.Count; i++)
        {
            vertices.Add(new Vector3(cell.Vertices[i].X, 0, cell.Vertices[i].Y));
        }

        // Строим треугольники методом "веера" (Fan triangulation) от центра к границам
        for (int i = 1; i < vertices.Count - 1; i++)
        {
            triangles.Add(0);
            triangles.Add(i);
            triangles.Add(i + 1);
        }
        // Закрывающий треугольник между последней точкой, первой граничной и центром
        triangles.Add(0);
        triangles.Add(vertices.Count - 1);
        triangles.Add(1);

        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        // Опционально: добавляем коллайдер, чтобы по ячейкам можно было кликать мыжкой или ходить физическим персонажам
        cellObj.AddComponent<MeshCollider>().sharedMesh = mesh;
    }
}


public class VoronoiGeometry
{
    public struct Vector2D
    {
        public float X;
        public float Y;
        public Vector2D(float x, float y) { X = x; Y = y; }
    }

    public class Cell
    {
        public Vector2D Site;              // Исходная точка центра
        public List<Vector2D> Vertices;   // Вершины границ полигона (вкруг центра)
        public Cell(Vector2D site)
        {
            Site = site;
            Vertices = new List<Vector2D>();
        }
    }

    // Простой класс треугольника для триангуляции Делоне
    private class Triangle
    {
        public Vector2D P1, P2, P3;
        public Vector2D Circumcenter;
        public float CircumradiusSquare;

        public Triangle(Vector2D p1, Vector2D p2, Vector2D p3)
        {
            P1 = p1; P2 = p2; P3 = p3;
            CalculateCircumcircle();
        }

        private void CalculateCircumcircle()
        {
            // Математический расчет центра описанной окружности
            float d = 2 * (P1.X * (P2.Y - P3.Y) + P2.X * (P3.Y - P1.Y) + P3.X * (P1.Y - P2.Y));
            if (Math.Abs(d) < 0.000001f) d = 0.000001f;

            float ux = ((P1.X * P1.X + P1.Y * P1.Y) * (P2.Y - P3.Y) + (P2.X * P2.X + P2.Y * P2.Y) * (P3.Y - P1.Y) + (P3.X * P3.X + P3.Y * P3.Y) * (P1.Y - P2.Y)) / d;
            float uy = ((P1.X * P1.X + P1.Y * P1.Y) * (P3.X - P2.X) + (P2.X * P2.X + P2.Y * P2.Y) * (P1.X - P3.X) + (P3.X * P3.X + P3.Y * P3.Y) * (P2.X - P1.X)) / d;

            Circumcenter = new Vector2D(ux, uy);
            CircumradiusSquare = (P1.X - ux) * (P1.X - ux) + (P1.Y - uy) * (P1.Y - uy);
        }

        public bool ContainsInCircumcircle(Vector2D p)
        {
            float distSq = (p.X - Circumcenter.X) * (p.X - Circumcenter.X) + (p.Y - Circumcenter.Y) * (p.Y - Circumcenter.Y);
            return distSq < CircumradiusSquare;
        }

        public bool HasEdge(Vector2D e1, Vector2D e2)
        {
            return (IsEqual(P1, e1) && IsEqual(P2, e2)) || (IsEqual(P2, e1) && IsEqual(P1, e2)) ||
                   (IsEqual(P2, e1) && IsEqual(P3, e2)) || (IsEqual(P3, e1) && IsEqual(P2, e2)) ||
                   (IsEqual(P3, e1) && IsEqual(P1, e2)) || (IsEqual(P1, e1) && IsEqual(P3, e2));
        }
    }

    private static bool IsEqual(Vector2D a, Vector2D b)
    {
        return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Y - b.Y) < 0.001f;
    }

    /// <summary>
    /// Главный метод: строит полигоны Вороного из набора точек алгоритмом Боуэра-Ватсона
    /// </summary>
    public static List<Cell> BowyerWatsonVoronoi(List<Vector2D> sites, float width, float height)
    {
        List<Cell> cells = new List<Cell>();
        if (sites.Count < 3) return cells;

        foreach (var site in sites) cells.Add(new Cell(site));

        // 1. Создаем супер-треугольник, покрывающий всю область карты с запасом
        Vector2D st1 = new Vector2D(width / 2, height * 3);
        Vector2D st2 = new Vector2D(-width * 2, -height);
        Vector2D st3 = new Vector2D(width * 3, -height);
        List<Triangle> triangulation = new List<Triangle> { new Triangle(st1, st2, st3) };

        // 2. Строим триангуляцию Делоне
        foreach (var site in sites)
        {
            List<Triangle> badTriangles = new List<Triangle>();
            foreach (var t in triangulation)
            {
                if (t.ContainsInCircumcircle(site)) badTriangles.Add(t);
            }

            List<KeyValuePair<Vector2D, Vector2D>> polygonEdges = new List<KeyValuePair<Vector2D, Vector2D>>();
            foreach (var t in badTriangles)
            {
                AddEdgeIfUnique(polygonEdges, t.P1, t.P2);
                AddEdgeIfUnique(polygonEdges, t.P2, t.P3);
                AddEdgeIfUnique(polygonEdges, t.P3, t.P1);
            }

            foreach (var t in badTriangles) triangulation.Remove(t);

            foreach (var edge in polygonEdges)
            {
                triangulation.Add(new Triangle(edge.Key, edge.Value, site));
            }
        }

        // 3. Конвертируем Делоне в Вороного (Центры окружностей треугольников становятся вершинами Вороного)
        foreach (var cell in cells)
        {
            // Ищем все треугольники, которые содержат эту центральную точку
            List<Triangle> sharedTriangles = new List<Triangle>();
            foreach (var t in triangulation)
            {
                if (IsEqual(t.P1, cell.Site) || IsEqual(t.P2, cell.Site) || IsEqual(t.P3, cell.Site))
                {
                    sharedTriangles.Add(t);
                }
            }

            // Центры описанных окружностей этих треугольников — это вершины нашей ячейки
            foreach (var t in sharedTriangles)
            {
                // Ограничиваем вершины рамками нашей сцены (клиппинг для простоты)
                float clampedX = Math.Clamp(t.Circumcenter.X, 0, width);
                float clampedY = Math.Clamp(t.Circumcenter.Y, 0, height);
                cell.Vertices.Add(new Vector2D(clampedX, clampedY));
            }

            // Сортируем вершины по часовой стрелке, чтобы меш правильно рендерился
            SortVerticesClockwise(cell);
        }

        return cells;
    }

    private static void AddEdgeIfUnique(List<KeyValuePair<Vector2D, Vector2D>> edges, Vector2D p1, Vector2D p2)
    {
        for (int i = edges.Count - 1; i >= 0; i--)
        {
            if ((IsEqual(edges[i].Key, p1) && IsEqual(edges[i].Value, p2)) || (IsEqual(edges[i].Key, p2) && IsEqual(edges[i].Value, p1)))
            {
                edges.RemoveAt(i);
                return;
            }
        }
        edges.Add(new KeyValuePair<Vector2D, Vector2D>(p1, p2));
    }

    private static void SortVerticesClockwise(Cell cell)
    {
        cell.Vertices.Sort((a, b) =>
        {
            float angleA = (float)Math.Atan2(a.Y - cell.Site.Y, a.X - cell.Site.X);
            float angleB = (float)Math.Atan2(b.Y - cell.Site.Y, b.X - cell.Site.X);
            return angleA.CompareTo(angleB);
        });
    }
}





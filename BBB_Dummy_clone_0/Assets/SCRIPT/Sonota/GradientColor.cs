using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class GradientColor : BaseMeshEffect
{
    private enum Direction
    {
        Horizontal = 0,
        Vertical = 1
    }

    [SerializeField]
    private Gradient gradient = default;

    [SerializeField]
    private Direction direction = default;

    public override void ModifyMesh(VertexHelper vertexHelper)
    {
        // 変更前頂点取得
        var baseVertices = new List<UIVertex>();
        vertexHelper.GetUIVertexStream(baseVertices);

        // 最小頂点位置／最大頂点位置
        var minVertexPosition = direction is Direction.Horizontal ? baseVertices.Min(x => x.position.x) : baseVertices.Min(x => x.position.y);
        var maxVertexPosition = direction is Direction.Horizontal ? baseVertices.Max(x => x.position.x) : baseVertices.Max(x => x.position.y);

        // グラデーションのキー位置
        var gradientKeyTimes = gradient.colorKeys.Select(x => x.time).Concat(gradient.alphaKeys.Select(x => x.time)).Concat(new[] { 0.0F, 1.0F }).OrderBy(x => x).ToHashSet();
        var gradientVertexPositions = gradientKeyTimes.Select(x => Mathf.Lerp(minVertexPosition, maxVertexPosition, x)).OrderBy(x => x).ToHashSet();

        // 重複頂点位置削除
        foreach (var vertexPosition in baseVertices.Select(x => x.position))
        {
            gradientVertexPositions.Remove(direction is Direction.Horizontal ? vertexPosition.x : vertexPosition.y);
        }

        // 頂点変更
        var vertices = new List<UIVertex>();

        for (var index = 0; index < baseVertices.Count; index += 6)
        {
            var targetVertices = baseVertices.Skip(index).Take(6).ToArray();
            var targetMinVertexPosition = direction is Direction.Horizontal ? targetVertices[0].position.x : targetVertices[0].position.y;
            var targetMaxVertexPosition = direction is Direction.Horizontal ? targetVertices[3].position.x : targetVertices[3].position.y;
            var targetGradientVertexPositions = gradientVertexPositions.Where(x => x > targetMinVertexPosition && x < targetMaxVertexPosition).OrderBy(x => x).ToArray();
            if (targetGradientVertexPositions.Length > 0)
            {
                var temporary = new List<UIVertex>();

                for (var target = 0; target < targetGradientVertexPositions.Length; target++)
                {
                    if (target > 0)
                    {
                        targetVertices = temporary.Skip(target * 6).Take(6).ToArray();

                        foreach (var _ in Enumerable.Range(0, 6))
                        {
                            temporary.RemoveAt(temporary.Count - 1);
                        }
                    }

                    var modifiedVertices = ModifyVertices(targetVertices, direction, targetGradientVertexPositions[target]);
                    temporary.AddRange(modifiedVertices);
                }

                vertices.AddRange(temporary);
            }
            else
            {
                vertices.AddRange(targetVertices);
            }
        }

        // 頂点カラー設定
        for (var index = 0; index < vertices.Count; index++)
        {
            var vertexPosition = direction is Direction.Horizontal ? vertices[index].position.x : vertices[index].position.y;
            var vertexPositionTime = (vertexPosition - minVertexPosition) / (maxVertexPosition - minVertexPosition);

            var vertex = new UIVertex();
            vertex.position = vertices[index].position;
            vertex.uv0 = vertices[index].uv0;
            vertex.color = gradient.Evaluate(vertexPositionTime);

            vertices[index] = vertex;
        }

        // 頂点情報更新
        vertexHelper.Clear();
        vertexHelper.AddUIVertexTriangleStream(vertices);
    }

    private static UIVertex[] ModifyVertices(UIVertex[] source, Direction direction, float gradientVertexPosition)
    {
        var positionX = (min: source[0].position.x, max: source[3].position.x);
        var positionY = (min: source[0].position.y, max: source[3].position.y);
        var uvX = (min: source[0].uv0.x, max: source[3].uv0.x);
        var uvY = (min: source[0].uv0.y, max: source[3].uv0.y);

        var result = new UIVertex[12];

        result[0] = source[0];  // 左下頂点 1 => 1
        result[5] = source[5];  // 左下頂点 6 => 6
        result[8] = source[2];  // 右上頂点 3 => 9
        result[9] = source[3];  // 右上頂点 4 => 10

        if (direction is Direction.Horizontal)
        {
            result[1] = source[1];   // 左上頂点 2 => 2
            result[10] = source[4];  // 右下頂点 5 => 11

            var uvPosition = Mathf.InverseLerp(positionX.min, positionX.max, gradientVertexPosition);
            var uv = Mathf.Lerp(uvX.min, uvX.max, uvPosition);

            var topVertex = new UIVertex();
            topVertex.position = new Vector3(gradientVertexPosition, positionY.max, 0.0F);
            topVertex.uv0 = new Vector4(uv, uvY.max, 0.0F, 0.0F);
            result[2] = topVertex;  // 上側追加頂点 3
            result[3] = topVertex;  // 上側追加頂点 4
            result[7] = topVertex;  // 上側追加頂点 8

            var bottomVertex = new UIVertex();
            bottomVertex.position = new Vector3(gradientVertexPosition, positionY.min, 0.0F);
            bottomVertex.uv0 = new Vector4(uv, uvY.min, 0.0F, 0.0F);
            result[4] = bottomVertex;   // 下側追加頂点 5
            result[6] = bottomVertex;   // 下側追加頂点 7
            result[11] = bottomVertex;  // 下側追加頂点 12
        }
        else
        {
            result[7] = source[1];  // 左上頂点 2 => 8
            result[4] = source[4];  // 右下頂点 5 => 5

            var uvPosition = Mathf.InverseLerp(positionY.min, positionY.max, gradientVertexPosition);
            var uv = Mathf.Lerp(uvY.min, uvY.max, uvPosition);

            var leftVertex = new UIVertex();
            leftVertex.position = new Vector3(positionX.min, gradientVertexPosition, 0.0F);
            leftVertex.uv0 = new Vector4(uvX.min, uv, 0.0F, 0.0F);
            result[1] = leftVertex;   // 左側追加頂点 2 
            result[6] = leftVertex;   // 左側追加頂点 7 
            result[11] = leftVertex;  // 左側追加頂点 12

            var rightVertex = new UIVertex();
            rightVertex.position = new Vector3(positionX.max, gradientVertexPosition, 0.0F);
            rightVertex.uv0 = new Vector4(uvX.max, uv, 0.0F, 0.0F);
            result[2] = rightVertex;   // 右側追加頂点 3  
            result[3] = rightVertex;   // 右側追加頂点 4  
            result[10] = rightVertex;  // 右側追加頂点 11 
        }

        return result;
    }
}
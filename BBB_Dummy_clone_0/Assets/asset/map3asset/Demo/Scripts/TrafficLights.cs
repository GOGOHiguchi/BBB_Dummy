using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum LightColor { Red, Yellow, Green, None }

public class TrafficLights : MonoBehaviour {

    public LightColor activeLight;
    private MeshRenderer mr;
    private Shader defShader, unlitShader;

    private void Start() {
        mr = GetComponent<MeshRenderer>();

        // URP用のシェーダーを取得
        defShader = Shader.Find("Universal Render Pipeline/Lit");
        unlitShader = Shader.Find("Universal Render Pipeline/Unlit");

        SetLight(activeLight);
    }

    public void SetLight(LightColor color) {
        if (mr == null || mr.materials.Length < 4) return;

        // mat 1 : green, mat 2 : yellow, mat 3 : red
        int activeIndex = 0;
        switch (color) {
            case LightColor.Green:
                activeIndex = 1;
                break;

            case LightColor.Yellow:
                activeIndex = 2;
                break;

            case LightColor.Red:
                activeIndex = 3;
                break;
        }

        for (int i = 1; i < 4; i++) {
            if (i < mr.materials.Length) {
                // 点灯しているライトはUnlit（発光）、消灯しているライトはLit（通常）
                Shader targetShader = (activeIndex == i) ? unlitShader : defShader;
                if (targetShader != null) {
                    mr.materials[i].shader = targetShader;
                }
            }
        }
    }
}
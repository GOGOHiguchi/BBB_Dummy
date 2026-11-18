using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BuildingLights : MonoBehaviour {
    public int windowMaterialIndex;
    public Color lightColor;
    public bool areLightsOn;
    private Color defaultColor;
    private MeshRenderer mr;

    private void Start() {
        mr = GetComponent<MeshRenderer>();
        
        // windowMaterialIndex の範囲チェック
        if (mr != null && mr.materials.Length > windowMaterialIndex)
        {
            defaultColor = mr.materials[windowMaterialIndex].color;
            SetLights(areLightsOn);
        }
    }

    public void SetLights(bool isOn) {
        if (mr == null || mr.materials.Length <= windowMaterialIndex) return;

        // URP用のシェーダーを指定する
        Shader lightShader = isOn ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        
        if (lightShader != null)
        {
            mr.materials[windowMaterialIndex].shader = lightShader;
        }

        mr.materials[windowMaterialIndex].color = isOn ? lightColor : defaultColor;
    }
}
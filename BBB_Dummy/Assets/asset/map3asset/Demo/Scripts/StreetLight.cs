using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StreetLight : MonoBehaviour {

    public Light[] lights;
    public bool isOn;

    private void Start() {
        SetLight(isOn);
    }

    public void SetLight(bool isOn) {
        this.isOn = isOn;

        MeshRenderer mr = GetComponent<MeshRenderer>();
        if (mr != null && mr.materials.Length > 1) {
            // URP用シェーダーを取得
            Shader shader = Shader.Find(isOn ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            
            if (shader != null) {
                mr.materials[1].shader = shader;
            }

            // ライトの色をマテリアルに反映（ライトが割り当てられている場合）
            if (lights != null && lights.Length > 0 && lights[0] != null) {
                mr.materials[1].color = lights[0].color;
            }
        }

        // ライトオブジェクトのON/OFF切替
        if (lights != null) {
            foreach (Light l in lights) {
                if (l != null) {
                    l.gameObject.SetActive(isOn);
                }
            }
        }
    }
}
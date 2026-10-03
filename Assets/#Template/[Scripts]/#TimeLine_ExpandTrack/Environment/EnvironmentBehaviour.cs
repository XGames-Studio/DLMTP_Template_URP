using System;
using UnityEngine;
using UnityEngine.Playables;

[Serializable]
public class EnvironmentBehaviour : PlayableBehaviour
{
    // 将 Gradient 改为 Trilight (三色模式) 更为贴切
    public enum RenderMode { Color, Trilight }

    [Tooltip("选择环境光模式")]
    public RenderMode mode = RenderMode.Color;

    // --- Color 模式参数 ---
    [ColorUsage(false, true)]
    public Color Ambient_Color = new(0.2f, 0.4f, 0.8f);

    // --- Trilight (原 Gradient) 模式参数 ---
    [Header("Trilight HDR Colors")]
    [Tooltip("对应 RenderSettings.ambientSkyColor")]
    [ColorUsage(false, true)]
    public Color Sky_HDR = Color.cyan;

    [Tooltip("对应 RenderSettings.ambientEquatorColor")]
    [ColorUsage(false, true)]
    public Color Equator_HDR = Color.gray;

    [Tooltip("对应 RenderSettings.ambientGroundColor")]
    [ColorUsage(false, true)]
    public Color Ground_HDR = Color.black;
}
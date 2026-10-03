using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public class EnvironmentMixerBehaviour : PlayableBehaviour
{
    private Color m_DefaultSky;
    private Color m_DefaultEquator;
    private Color m_DefaultGround;
    private AmbientMode m_DefaultMode;

    private bool m_FirstFrameHappened;

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (!m_FirstFrameHappened)
        {
            m_DefaultSky = RenderSettings.ambientSkyColor;
            m_DefaultEquator = RenderSettings.ambientEquatorColor;
            m_DefaultGround = RenderSettings.ambientGroundColor;
            m_DefaultMode = RenderSettings.ambientMode;
            m_FirstFrameHappened = true;
        }

        int inputCount = playable.GetInputCount();

        Color blendedSky = Color.clear;
        Color blendedEquator = Color.clear;
        Color blendedGround = Color.clear;
        float totalWeight = 0f;

        AmbientMode targetMode = AmbientMode.Flat;

        for (int i = 0; i < inputCount; i++)
        {
            float inputWeight = playable.GetInputWeight(i);
            if (inputWeight <= 0) continue;

            var inputPlayable = (ScriptPlayable<EnvironmentBehaviour>)playable.GetInput(i);
            EnvironmentBehaviour input = inputPlayable.GetBehaviour();

            // 修改点：直接使用 Color 乘权重，不再需要计算时间采样
            if (input.mode == EnvironmentBehaviour.RenderMode.Trilight)
            {
                targetMode = AmbientMode.Trilight;

                blendedSky += input.Sky_HDR * inputWeight;
                blendedEquator += input.Equator_HDR * inputWeight;
                blendedGround += input.Ground_HDR * inputWeight;
            }
            else
            {
                blendedSky += input.Ambient_Color * inputWeight;
                blendedEquator += input.Ambient_Color * inputWeight;
                blendedGround += input.Ambient_Color * inputWeight;
            }

            totalWeight += inputWeight;
        }

        if (totalWeight > 0f)
        {
            float invWeight = 1f - Mathf.Clamp01(totalWeight);
            RenderSettings.ambientMode = targetMode;
            RenderSettings.ambientSkyColor = blendedSky + m_DefaultSky * invWeight;
            RenderSettings.ambientEquatorColor = blendedEquator + m_DefaultEquator * invWeight;
            RenderSettings.ambientGroundColor = blendedGround + m_DefaultGround * invWeight;
        }
        else
        {
            RestoreDefaults();
        }
    }

    public override void OnPlayableDestroy(Playable playable)
    {
        m_FirstFrameHappened = false;
        RestoreDefaults();
    }

    private void RestoreDefaults()
    {
        RenderSettings.ambientMode = m_DefaultMode;
        RenderSettings.ambientSkyColor = m_DefaultSky;
        RenderSettings.ambientEquatorColor = m_DefaultEquator;
        RenderSettings.ambientGroundColor = m_DefaultGround;
    }
}
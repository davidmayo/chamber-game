using UnityEngine;

public sealed class SpectrumAnalyzerDisplay : MonoBehaviour
{
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

    [SerializeField] private Renderer screenRenderer;
    [SerializeField] private Texture defaultTexture;

    private MaterialPropertyBlock propertyBlock;

    public void Configure(Renderer renderer, Texture texture)
    {
        screenRenderer = renderer;
        defaultTexture = texture;
        SetTexture(defaultTexture);
    }

    public void SetTexture(Texture texture)
    {
        if (screenRenderer == null || texture == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        screenRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetTexture(BaseMapId, texture);
        screenRenderer.SetPropertyBlock(propertyBlock);
    }

    private void Awake()
    {
        SetTexture(defaultTexture);
    }
}

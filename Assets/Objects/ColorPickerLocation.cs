using UnityEngine;

namespace Objects
{

    public class ColorPickerLocation : MonoBehaviour
    {
        public MeshRenderer _renderer;
        public Color _originalColor;


        private void Start()
        {
            // Store the original color of the first material
            _originalColor = _renderer.sharedMaterials[0].color;
        }

        public void setRenderer(MeshRenderer renderer)
        {
            _renderer = renderer;
        }

        public Color GetCurrentColor()
        {
            // Return the current color of the first material
            return _renderer.sharedMaterials[0].color;
        }

        public void SetColor(Color color)
        {
            // Create a new array to avoid modifying the shared materials directly
            var materials = _renderer.materials;
            materials[0].color = color;
            _renderer.materials = materials;
        }

        public void ResetColor()
        {
            // Reset to the original color
            var materials = _renderer.materials;
            materials[0].color = _originalColor;
            _renderer.materials = materials;
        }

        public void SetColorFromHex(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                Debug.Log(color);
                SetColor(color);
            }
            else
            {
                Debug.LogWarning("Invalid hex string");
            }
        }
    }
}

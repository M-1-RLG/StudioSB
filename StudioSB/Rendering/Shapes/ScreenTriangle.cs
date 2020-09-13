﻿using System.Collections.Generic;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using SFGenericModel;
using SFGenericModel.VertexAttributes;
using SFGraphics.GLObjects.Textures;

namespace StudioSB.Rendering.Shapes
{
    class ScreenTriangle : GenericMesh<Vector3>
    {
        // A triangle that extends past the screen.
        private static List<Vector3> screenTrianglePositions = new List<Vector3>()
        {
            new Vector3(-1f, -1f, 0.0f),
            new Vector3( 3f, -1f, 0.0f),
            new Vector3(-1f,  3f, 0.0f)
        };

        private static ScreenTriangle triangle;

        public static void RenderTexture(Texture renderTexture, bool displayR, bool displayG, bool displayB, bool displayA, int LOD, bool IsSrgb = false)
        {
            if (triangle == null)
                triangle = new ScreenTriangle();

            // Texture unit 0 should be reserved for image preview.
            var shader = ShaderManager.GetShader("Texture");
            shader.UseProgram();
            if (renderTexture != null)
                shader.SetTexture("image", renderTexture, 0);
            
            // The colors need to be converted back to sRGB gamma.
            shader.SetBoolToInt("isSrgb", IsSrgb);

            bool monoChannel = false;
            if (displayR && !displayG && !displayB && !displayA)
                monoChannel = true;
            if (!displayR && displayG && !displayB && !displayA)
                monoChannel = true;
            if (!displayR && !displayG && displayB && !displayA)
                monoChannel = true;
            if (!displayR && !displayG && !displayB && displayA)
                monoChannel = true;

            shader.SetBoolToInt("enableR", displayR);
            shader.SetBoolToInt("enableG", displayG);
            shader.SetBoolToInt("enableB", displayB);
            shader.SetBoolToInt("enableA", displayA);
            shader.SetBoolToInt("monoChannel", monoChannel);
            shader.SetInt("LOD", LOD);

            triangle.Draw(shader);
        }

        public static void RenderTexture(Texture renderTexture, bool IsSrgb = false)
        {
            RenderTexture(renderTexture, true, true, true, true, 0, IsSrgb);
        }

        public ScreenTriangle() : base(screenTrianglePositions, PrimitiveType.Triangles)
        {

        }

        public override List<VertexAttribute> GetVertexAttributes()
        {
            return new List<VertexAttribute>()
            {
                new VertexFloatAttribute("position", ValueCount.Three, VertexAttribPointerType.Float, false),
            };
        }
    }
}

// FingerOccluder.shader
// Save as:  Assets/Shaders/FingerOccluder.shader
//
// Creates a material that:
//  • Writes to the depth buffer (ZWrite On)
//  • Does NOT write to the colour buffer (ColorMask 0)
//  • Renders in the Geometry queue BEFORE the ring (Queue = Geometry-1)
//
// Result: the finger capsule "blocks" the ring from showing where it goes
// behind the finger – giving a true wrap-around / occlusion effect.

Shader "Custom/FingerOccluder"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue"      = "Geometry-1"   // renders before ring
        }

        ColorMask 0    // write nothing to colour buffer
        ZWrite    On   // but DO write to depth buffer
        Cull      Off  // occlude from both sides of the capsule

        Pass { }
    }
}

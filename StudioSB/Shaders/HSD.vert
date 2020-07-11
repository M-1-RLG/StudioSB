﻿#version 330

in vec3 POS;
in vec3 NRM;
in vec2 UV0;
in vec4 Clr0;
in vec4 Bone;
in vec4 Weight;

out vec3 geomPosition;
out vec3 geomNormal;
out vec2 geomTex0;
out vec4 geomColor;
out vec3 geomBitangent;
out vec3 geomTangent;

out vec3 vertPosition;
out vec3 normal;
out vec2 tex0;
out vec4 color;
out vec3 bitangent;
out vec3 tangent;
out vec3 specularPass;

// specular----------------------------------
out vec3 geomSpecularPass;

uniform int hasSpecular;
uniform int specularCoordType;
uniform vec2 specularScale;
uniform sampler2D specularTex;

uniform vec4 specularColor;
uniform mat4 sphereMatrix;

uniform float glossiness;
uniform vec3 cameraPos;
uniform int enableSpecular;

// end specular----------------------------------

uniform BoneTransforms
{
    mat4 transforms[200];
} bones2;

uniform mat4 binds[200];

uniform mat4 singleBind;
uniform mat4 mvp;


vec2 GetSphereCoords(vec3 N)
{
    vec3 viewNormal = mat3(sphereMatrix) * N;
    return viewNormal.xy * 0.5 + 0.5;
}

vec2 GetCoordType(int coordType, vec2 tex0)
{
	//COORD_REFLECTION
	if(coordType == 1)
		return GetSphereCoords(NRM);
	//COORD_UV
	return tex0;
}

vec3 ColorMapSpecularPass(vec3 N, vec3 V)
{
    vec3 specularTerm = vec3(1);
	
    vec2 Coords = GetCoordType(specularCoordType, UV0);

    if (hasSpecular == 1)
        specularTerm = texture(specularTex, Coords * specularScale).rgb;

    return specularTerm;
}

vec3 SpecularPass(vec3 N, vec3 V)
{
    float phong = clamp(dot(N, V), 0, 1);

    phong = pow(phong, glossiness);

    vec3 specularTerm = vec3(phong) * specularColor.rgb;

	if(enableSpecular == 0)
		specularTerm = vec3(0);

    return specularTerm;
}

void main()
{
	vec3 position = (singleBind * vec4(POS, 1)).xyz;
	normal = (inverse(transpose(singleBind)) * vec4(NRM, 1)).xyz;
	if(Weight.x == 1)
	{
		position = (bones2.transforms[int(Bone.x)] * vec4(position, 1)).xyz;
		normal = (inverse(transpose(bones2.transforms[int(Bone.x)])) * vec4(normal, 1)).xyz;
	}
	else
	if(Weight.x > 0 || Weight.y > 0 || Weight.z > 0 || Weight.w > 0)
    {
        vec4 transformedPosition = vec4(position, 1);
        vec4 transformedNormal = vec4(0);
		position = vec3(0);

        for (int i = 0; i < 4; i++)
        {
			position += (binds[int(Bone[i])] * transformedPosition * Weight[i]).xyz;
			transformedNormal.xyz += (inverse(transpose(binds[int(Bone[i])])) * vec4(normal, 1) * Weight[i]).xyz;
        }

		normal = transformedNormal.xyz;
    }

	vertPosition = position;
	tex0 = UV0;
	color = Clr0;
	tangent = vec3(0);
	bitangent = vec3(0);
	vec3 V = normalize((position.xyz - cameraPos));
	specularPass = ColorMapSpecularPass(NRM, V) * SpecularPass(NRM, V);
    gl_Position = mvp * vec4(position, 1);
}
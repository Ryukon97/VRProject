/* ------------------------------------------------------------
 * AlternativeFull
 * ------------------------------------------------------------ */
/* created by AlternativeFullFrontend. */
#define TEXTURE_THRESHOLD "shading_0.png"
#define TEXTURE_SHADOW "Coat_S.png"
#define USE_SELFSHADOW_MODE
#define USE_NONE_SELFSHADOW_MODE
#define USE_HIGHLIGHT_CHEET
float HighlightPower = 0.5;
#define USE_HIGHLIGHT_COLOR_TYPE1
#define USE_SOFT_SHADOW
float SoftShadowParam = 10;
float SelfShadowPower = 0;
#define HIGHLIGHT_ANTI_AUTOLUMINOUS
#define USE_MATERIAL_SPECULAR
#define USE_MATERIAL_SPHERE
float3 DefaultModeShadowColor = {1,1,1};
#define MAX_ANISOTROPY 16

#include "AlternativeFull.fxsub"

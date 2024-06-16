UnityVolumetricCloudsURP
=============
 
 Volumetric Clouds for Unity URP (Universal Render Pipeline). The rendering is ported from HDRP.
 
 **Please read the Documentation and Requirements before using this repository.**
 
Screenshots
------------
**Sample Scene**
 
Global Volumetric Clouds:
 
 ![GlobalVolumetricClouds](./Documentation/Images/Showcases/GlobalClouds.jpg)
 
**Not Included**
 
Local Volumetric Clouds:
 
 ![LocalVolumetricClouds1](./Documentation/Images/Showcases/LocalClouds1.jpg)
 
 ![LocalVolumetricClouds2](./Documentation/Images/Showcases/LocalClouds2.jpg)
 
Documentation
------------
- [How to setup](./Documentation/Setup.md)
 
Requirements
------------
- Unity 2022.2 and URP 14 or above.
- Shader model 3.5 or above (at leaset OpenGL ES 3.0 or equivalent)
 
Reminders
------------
- Some settings are still WIP, such as **Custom Cloud Map** overrides.
- Orthographic camera is not supported.
- To render volumetric clouds shadows, it will override the cookie in the main directional light.
 
License
------------
MIT
![MIT License](http://img.shields.io/badge/license-MIT-blue.svg?style=flat)
 
Details
------------
Please refer to [HDRP Volumetric Clouds Documentation](https://docs.unity3d.com/Packages/com.unity.render-pipelines.high-definition@14.0/manual/Override-Volumetric-Clouds.html#properties).
 
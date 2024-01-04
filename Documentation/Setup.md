Documentation
=============

Setup
-------------

- Add **Volumetric Clouds URP** Renderer Feature to the active URP Renderer asset.

 ![AddRendererFeature](./Documentation/Images/Settings/URP_RendererFeature_VolumetricClouds.jpg)

- Add **Sky/Volumetric Clouds (URP)** to the scene's URP Volume.

- Set the **State** to **Enabled** in Volumetric Clouds overrides.

 ![AddVolumeOverride](./Documentation/Images/Settings/URP_VolumeOverride_VolumetricClouds.jpg)

- Adjust the settings in URP Volume and use different Volume types (global and local) to control volumetric clouds if needed.

- For local volumetric clouds, increase the far plane of **camera** or **reflection probe** to render distant clouds.

 ![AdjustFarPlane](./Documentation/Images/Settings/URP_SceneCamera_FarPlane.jpg)

- On platforms that don't implement reversed-z (ex. OpenGL), please keep the camera near plane (ex. 0.1) high to avoid depth precision issues.
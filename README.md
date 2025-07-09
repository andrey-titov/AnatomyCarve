# AnatomyCarve
AnatomyCarve is a Unity package that allows interactive visualization of 3D medical images by enabling users to perform clipping on user-selected segments using a virtual reality (VR) headset.

It allows users to selectively clip specific segments, enabling the creation of detailed visualizations comparable to those found in anatomical textbooks. By combining the strengths of clipping and deformation methods, the algorithm enhances the visibility of hidden 3D anatomical structures without compromising their original positions and spatial relationships.

With AnatomyCarve, users begin by selecting specific anatomical segments within the segmented volume, marking some segments as unclippable, meaning that they remain fully visible. Simultaneously, the user inserts clipping shapes into the volume, which hides the voxels of all segments that were not selected previously.

Once a clipping shape is fixed within the volume, it continues to clip voxels from segments not marked as unclippable, allowing users to introduce additional clipping shapes sequentially for further detailed carving. The interactive process is asymmetric, assigning distinct roles to each hand for enhanced usability. The non-dominant hand places invisible clipping spheres into the volume, while the dominant hand employs a laser pointer to accurately select segments to toggle between clippable and unclippable states. Each selected segment visually indicates its current status by changing colors between green (unclippable) and red (clippable).

Users can conveniently toggle segment clipping status by pressing triggers on the controller, quickly adjusting their visualizations. Additionally, a secondary controller button enables users to reset the clipping states, either enabling or disabling clipping for all segments simultaneously. The system further supports repositioning the entire volume within the scene for improved visualization. Designed to accommodate both right-handed and left-handed users, AnatomyCarve allows easy swapping of hand roles, enhancing accessibility and comfort during prolonged interactions.

![image](https://github.com/user-attachments/assets/6459885e-d5f9-4910-ac11-42f1df4a7c1c)

## Installation
1) Create or open a project in Unity 2022.3 or later.
2) Navigate to *Window => Package Manager*. The *Package Manager* window should open.
3) Click on the small plus in the top left of the *Package Manager* window, and then click on **Add Package from git URL...**

![image](https://github.com/andrey-titov/ContextualAO/assets/22062174/600bceb2-5238-411c-8f51-7f2542ff1c5b)

4) Paste `https://github.com/andrey-titov/AnatomyCarve.git` and click on **Add**.
5) Wait for the package to be downloaded and installed. When this is done, the *AnatomyCarve* package should then appear in the list inside the *Package Manager* window.
6) Select the **AnatomyCarve** package in the list of packages, click on **Samples** an then on **Import** next to *Full Setp*, then *Demo Data* and finally *Demo Segment Carving VR*.
7) Navigate to *Edit => Project Settings*. The *Project Settings* Window should open.
8) Navigate to *Tags and Layers* and click one th *2 mini-sliders* icon on the top right.

![image](https://github.com/andrey-titov/ContextualAO/assets/22062174/aeeae63e-4428-4dcc-acc6-9b9f06fc61a1)

10) Click on the **AC** preset.
11) In the *Project* window, navigate to *Assets => Samples => AnatomyCarve \*.\*.\* => Demo Data*.
12) Copy or move the the *StreamingAssets* folder inside *Demo Data* to the root *Assets* folder of the unity project, so that it is located in **Assets/StreamingAssets**
13) Open the *Segmented Rendering* scene in *Assets => Samples => AnatomyCarve \*.\*.\* => Demo Segment Carving VR* to open a sample scene that can be run with a VR headset.

# References
[AnatomyCarve: A VR occlusion management technique for medical images based on segment-aware clipping](https://arxiv.org/abs/2507.05572)


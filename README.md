# AnatomyCarve
AnatomyCarve is a Unity package that allows interactive visualization of 3D medical images by enabling users to perform clipping on user-selected segments using a virtual reality (VR) headset.

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

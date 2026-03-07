# Jewellery AR Project

This project is an **Augmented Reality (AR) Jewellery Try-On Application** built using **Unity**.  
It allows users to virtually try jewellery using their device camera through AR.

---

## Requirements

Before running the project, make sure the following software is installed:

- **Unity Hub**
- **Unity Editor (Unity 2023 LTS or Unity 6 recommended)**
- **Android Build Support** (if building for Android)

---

## Unity Packages Used

This project uses the following Unity packages:

- **AR Foundation**
- **ARCore XR Plugin**
- **glTFast** (for loading `.glb` / `.gltf` 3D models)
- **Input System**
- **Unity UI (UGUI)**

All required packages are defined in the project’s `Packages/manifest.json` file and will be installed automatically when the project is opened in Unity.

---

## Clone the Repository

Clone the project using Git:

```bash
git clone https://github.com/kirtanpatel35367/Jewellery-AR-Project.git
```

---

## Open the Project

1. Open **Unity Hub**
2. Click **Add Project**
3. Select the cloned repository folder
4. Open the project using the recommended Unity version

Unity will automatically import assets and install required dependencies.

---

## Running the Project

1. Open the main scene:

```
Assets → Scenes → FaceTryOn
```

2. Click **Play** in the Unity Editor.

The AR system will start and the camera will activate.

---

## Build for Android (Optional)

To run the project on an Android device:

1. Open:

```
File → Build Settings
```

2. Select **Android**
3. Click **Switch Platform**

Then enable ARCore:

```
Edit → Project Settings → XR Plugin Management
```

Enable:

```
ARCore
```

Finally click:

```
Build & Run
```

---

GitHub:  
https://github.com/kirtanpatel35367

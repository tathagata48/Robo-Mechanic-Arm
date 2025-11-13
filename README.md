# Robo Mechanic Arm – Vision Driven Pick & Place

This project wires a Unity robot arm scene to a Python OpenCV vision server. The main
camera streams images to Python, which detects red cubes and instructs the robot to
pick and place them at a predefined drop-off position.

## Project Structure

```
Assets/
  Scenes/               # Unity scenes
  Scripts/              # Runtime scripts for networking, spawning and robot control
python/
  vision_server.py      # OpenCV-powered TCP vision server
```

## Unity Setup

1. **Scene Objects**
   - Add the `CameraStreamer` component to the main camera.
   - Add the `VisionCommandRouter` and `RobotPickAndPlaceController` scripts to suitable
     game objects (typically the robot root).
   - Assign references in the inspector:
     - `CameraStreamer.commandRouter` → the `VisionCommandRouter` instance.
     - `VisionCommandRouter.robotController` → the `RobotPickAndPlaceController`.
     - `RobotPickAndPlaceController` requires:
       - `Robot Root`: transform used to translate/rotate the robot towards cubes.
       - `Drop Off Point`: empty transform indicating where cubes are released.
       - `Grip Attachment Point`: transform on the gripper used to parent picked cubes.
       - `Robot Animator`: animator with a trigger matching `Pick Trigger Name` (default `Pick`).
       - `Pick Animation Duration`: duration of the pick animation clip in seconds (match the
         included animation clip).
   - Place a `CubeSpawner` in the scene and set its spawn area to match the ground plane.

2. **Animation**
   - Use the provided pick-and-place animation clip within the animator controller. Ensure the
     trigger name matches the controller configuration.

3. **Tags**
   - The project defines the `RedCube` tag. The spawner automatically assigns it, and the robot
     searches for objects with this tag when executing commands from Python.

4. **Networking**
   - By default the Unity client connects to `127.0.0.1:5005`. Adjust host/port on the
     `CameraStreamer` component if the Python server runs elsewhere.

## Python Vision Server

Install dependencies and run the server before hitting play in Unity:

```bash
pip install -r requirements.txt  # optional helper, see below
python python/vision_server.py --display
```

The server receives JPEG frames, detects red regions in HSV colour space, and returns
`movered` when a sufficiently large red region is visible. Unity interprets this command
by moving the robot to the closest red cube, playing the pick animation, and dropping the
cube at the drop-off point. When no red is detected, the server replies with `idle`.

If you prefer, create a `requirements.txt` with `opencv-python` and `numpy` or install
these packages manually.

## Extending

- Adjust the cube spawn area or count via the `CubeSpawner` component.
- Expand `VisionCommandRouter` to react to additional commands from Python (for example,
  change robot speed or reset positions).
- Enhance the Python server to identify cube positions and send coordinates rather than a
  simple trigger command.

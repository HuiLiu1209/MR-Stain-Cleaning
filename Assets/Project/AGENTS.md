# MR Stain Cleaning Unity Project

这是一个用 Meta Quest 与 Unity 识别地面污渍并生成 Prefab 的项目

项目的目标是：

1.使用 MRUK 扫描真是房间并获取 Floor Plane;
2.使用 Meta Building Blocks 里面中的 Object Detection 来检测地板上的污渍；
3.使用 Grid Placement System 在 Floor Plane 上生成 grid system；

## Project Structure

主要项目代码放在：
`Assets/Project/`

不要把项目代码直接放在：

`Assets/`
`Assets/MetaXR/`
`Assets/Oculus/`
`Assets/HypertonicGames/`
`Assets/ThirdParty/`

## Core Architecture
### MRUKFloor

路径：

Assets/_Project/Scripts/MRUKFloor/

职责：

等待 MRUK scene loaded
获取 floor anchor / floor plane
输出 FloorPlaneData
不负责目标检测
不负责生成 grid
不负责生成 stain prefab

### Detection

路径：

Assets/_Project/Scripts/Detection/

职责：

对接 Meta Building Blocks object detection
加载或引用 YOLO11n 模型
输出 DetectionResult
不负责生成 grid
不负责修改 grid cell state
不负责生成 stain prefab

### Grid

路径：

Assets/_Project/Scripts/Grid/

职责：

根据 FloorPlaneData 在 floor 上生成 grid
维护每个 GridCellData
提供 world position 到 grid cell 的转换
维护 grid cell state
不直接读取 MRUK anchor
不直接运行 object detection
不直接生成 stain prefab

### StainSpawning

路径：

Assets/_Project/Scripts/StainSpawning/

职责：

接收 DetectionResult
查询 FloorGridManager
找到对应的 grid cell
将对应 cell 标记为 HasStain
在 cell center 生成 stain prefab
避免重复生成 prefab

## Coding Rules
### General

使用 C# 编写 Unity scripts。
每个 class 只负责一个明确职责。
不要把 MRUK、Detection、Grid、Prefab Spawning 混在同一个脚本里。
不要 hardcode prefab path。
不要 hardcode scene object name，除非只是 debug / prototype。
不要修改 Assets/MetaXR/、Assets/Oculus/、Assets/ThirdParty/，除非用户明确要求。
不要删除用户已经实验成功的 prototype 文件。

### Debug Logs

Debug log 要有明确 prefix。

推荐：
Debug.Log("[MRUKFloor] Floor found.");
Debug.Log("[Grid] Grid generated.");
Debug.Log("[Detection] Stain detected.");
Debug.Log("[StainSpawn] Spawned stain prefab.");

不要使用不清楚的 log：
Debug.Log("done");
Debug.Log("test");
Debug.Log("here");

## Before Making Changes

在修改代码前，先判断：

这个功能属于哪个模块？
有没有 existing working reference？
是否需要新建 class，而不是塞进已有 class？
是否会破坏已经跑通的 prototype？
是否违反了模块边界？

如果不确定，优先保持改动小，并说明原因。


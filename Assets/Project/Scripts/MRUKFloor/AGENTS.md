# Detection Module

这个文件夹只负责 stain detection / object detection。

项目里的检测系统目标是：

使用 Meta Building Blocks 的 object detection 功能，并结合用户提供的 YOLO11n stain detection model，检测地板上的污渍。

## Responsibilities

这个模块负责：

- 对接 Meta Building Blocks object detection。
- 使用对应的 Inference Engine 依赖
- 加载或引用 YOLO11n stain detection model。
- 将模型转化成对应的 .sentis 文件
- 处理 label / confidence threshold。
- 将 raw detection output 转换成统一的 `DetectionResult`。
- 将检测到的污渍结果提供给其他系统。


## Reference

将 ONNX 文件转化成 .sentis 的工具可以参考
`D:\UnityProjects\My project`
仅可参考 文件格式转化 以及 物体识别的部分

不要参考这个路径下的 Grid System 的部分
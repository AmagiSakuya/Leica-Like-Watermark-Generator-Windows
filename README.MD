# 徕卡水印生成器-Windows
<p align="center">
  <img src="./screenshot/icon.png" alt="项目图标" width="150"/>
</p>

<div align="center">
  <img src="https://img.shields.io/github/license/AmagiSakuya/Leica-Like-Watermark-Generator-Windows" alt="license">
  <img src="https://img.shields.io/badge/dotnet-%3E%3D6.0-blue" alt="build">
  <img src="https://img.shields.io/badge/Twitter-@昨夜丶-1DA1F2?logo=twitter" alt="coverage">
</div>

## 简介
Windows下的Leica Like Watermark生成器，用于批量处理相机产生的图片，更符合相机的工作流。
可以用配置文件来自定义机型、Logo、图片输出质量、其他参数。
对于无EXIF的图片，可以运行时选择一些默认项或者手动输入来生成。

![Dotnet Version](./screenshot/1.gif)

## 使用方法

01.将图片(jpg)存放在 [程序目录/UserData/Input/(对应机型)]
02.运行程序，读取到图片，按任意键继续开始处理

程序基于net6.0开发 请确保运行环境的dotNet版本
更多高级参数 可以在[程序目录]-[config.json] 中设置

## 配置文件参数 
> [程序目录]-[config.json]
-- -- --

| 参数名          | 说明     |
| ---------  | ------- |
| IgnoreLensModel       | 从EXIF中读出这些镜头时忽略，让用户处理 (如老蛙镜头 可能会写入"----"这样的数据)   |
| NoExifImageConfig       | 无法读取Exif信息时的处理方式配置 |
| --UsePresetInsteadRealtimeUserSelection     | 是否使用预设值，不用在运行时让用户一个一个选择 |
| --Preset      | 预设值 包含【厂商】【机型】【镜头】【焦段】【ISO】【快门速度】【光圈】【时间】 |
| --ModelOptions      | 当不使用预设值时候，用户可以选择的【机型】选项 |
| --LensModelOptions      | 当不使用预设值时候，用户可以选择的【镜头】选项 |
| --FocalLengthOptions      | 当不使用预设值时候，用户可以选择的【焦段】选项 |
| --ShutterSpeedValueOptions     | 当不使用预设值时候，用户可以选择的【快门速度】选项 |
| --ApertureValueOptions       | 当不使用预设值时候，用户可以选择的【光圈】选项 |
| --ISOSpeedRatings      | 当不使用预设值时候，用户可以选择的【ISO】选项 |
| EquivalentFocalLength       | 等效焦距设置  |
| --Current     | 该值必须是【DefineCoefficient】中定义的某个【CoefficientName】其中一个，代表乘上配置的系数 |
| --DefineCoefficientStruct      | 等效焦距配置 |
| ----DefineCoefficientStruct.CoefficientName      | 等效焦距名称 |
| ----DefineCoefficientStruct.Coefficient      | 等效焦距系数 |
|SaveJPG_Quality       | 输出JPG图片质量 |
# createDEM
## Introduction
- 基于AutoCAD和C#的二次开发，调用AutoCAD程序集，并在AutoCAD中通过代码计算并绘制不规则三角网（TIN）与等高线 
- 测试于`AutoCAD 2014`与`VS2010`

## Trouble shooting
可能由于文件路径或者CAD版本问题，会出现一些情况，下面是解决方法，如果没有问题的话，下面可以忽略：  
- 打开项目，将references下无用的删除，然后添加“程序集”中的内容，它们都来自计算机安装目录，为了方便我将它们复制出来了；  
- CAD 2012版本的话，不要添加程序集中的 “accoremgd”，因为它只适用于CAD 2014；  
- 程序启动，使用CAD 安装目录下的 AutoCAD.exe；  
- 运行时会打开CAD，然后输入命令netload，添加debug文件夹下的dll，然后就可以运行自定义命令了；  
- 输入命令”DEM”，实现由点云生成TIN再生成等高线。

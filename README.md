# PipeNet
Visualize pipe networks and do some related analyses in Unity3D

![](https://github.com/LizhuWeng/PipeNet/blob/master/Doc/sample1.gif)
------------------------------------------------------
# QUICK START
Once you have installed Pipe Net with the 'PipeNet' directory in your project view.

To start creating your new pipe network, click on GameObject > Create Other > PipeNet
This will place a new pipe network game object to the scene and also create a horizontal plane for collision checking when adding a new node(You can also delete that and then use your own colliders).

![](https://github.com/LizhuWeng/PipeNet/blob/master/Doc/editor.gif)

------------------------------------------------------
BASIC OVERVIEW:
------------------------------------------------------

![](https://github.com/LizhuWeng/PipeNet/blob/master/Doc/inspector.JPG)

------------------------------------------------------
Global Setting: 

	Show Flow Line	- switch to show the flowing line animation effects.
	Show Sign		- switch to show the node signs (denote which node is valve or source .etc).
	Attach To Ground- should node be attached to the hit collider's point or not?

	Line Width		- pipe lines' width
	Pipe Scale		- pipe objects' width

	UV Opitions:	- custom set UV options

	Main Material:	- main pipe line material
	Block Material:	- pipe line material when pipe is blocked
	Pipe Material:	- pipe objects' material

Current Node:
	When you has selected one node, you can change it's node type(Common /Valve /Source)

Edit Mode:
	Transform:		- you can move or delete nodes. With 'CTRL' key down do a continuous connection
	Connect Line	- you can drag a node to the other to connect them.

------------------------------------------------------
TESTED UNITY VERSION:
------------------------------------------------------
> Unity 5.3 or Newer (2018.3 .etc)

------------------------------------------------------
ANALYSES:
------------------------------------------------------

![](https://github.com/LizhuWeng/PipeNet/blob/master/Doc/analysis1.gif)
![](https://github.com/LizhuWeng/PipeNet/blob/master/Doc/analysis2.gif)

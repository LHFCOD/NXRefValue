   M S B A M L   `   `   `  ���� F  BNXBaseValue, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null)clr-namespace:NXBaseValueNXBaseValue  E ALHFControl, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null;,clr-namespace:LHFControl;assembly=LHFControl
LHFControl     NXBaseValue.MainWindow   R NWindowsBase, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35W SPresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35D 9http://schemas.microsoft.com/winfx/2006/xaml/presentation    R NSystem.Xaml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     @d2http://schemas.microsoft.com/expression/blend/2008     Jmc;http://schemas.openxmlformats.org/markup-compatibility/2006     +localclr-namespace:NXBaseValue     C
LHFControl,clr-namespace:LHFControl;assembly=LHFControl       � Title$  基准值计算软件��$��437.5��$	��665�� � Icon$ icon.png��.��� $��Center=�$��Center=�$	��484��$	��220��$��15,76,18,51q� � ColumnDefinitions �� $
��307* ��� $
��180* �.+���  �� Orientation$ Vertical=�$��0��$��Right=�$	��305��.+��� $ 
Horizontal=�.+��� $��Center=�$��20(�聚类数目�� -   $��	num_julei��$��Center=�$��60��$��20(� �� TextAlignment$ Center=�.��3�� $��Center=�$��20(�聚类百分比�� -    ��Name$ zhichidu��$��Center=�$��60��$��20(�$ Center=�$	��0.3���� -   $��70��$��34��$
��Left=�$��0,14,0,0q�$��20(�$y�Visible=�.��计算�� $ 
Horizontal=�.+��� -    J� RenderTransformOrigin$ 0.5,0.5&�$	��134��$��20(�.��打开配置文件�� -   $��37��$��0,0,0,0q�$��20(�$��0q�$��1q�$y�Visible=�$	��166��.��根据配置进行计算�� -   $��display_isFindFile��$��24(�未打开配置文件�� -   $��WaitingPanel��$��60��$ 
Horizontal=�$
��Left=�$y�	Collapsed=�.+�  LHFControl.WaitingBox  -   $��
waitingbox��$��21���� -	   $��WaitingText��$��正在计算��$��24(�$��Center=��� $��1�����	#FF5F5F5F$��3q�._��� -
   $��Scroll�����	#FFF01F1F$��0q�.���� -   $��detail��$
��Wrap=�$��0,0,0,75q�$��	Condensed&�
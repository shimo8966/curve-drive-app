using System;using System.Runtime.InteropServices;
[ComImport,Guid("618736E0-3C3D-11CF-810C-00AA00389B71"),InterfaceType(ComInterfaceType.InterfaceIsDual)]
interface CadAccessible {
 [return:MarshalAs(UnmanagedType.IDispatch)] object Parent();
 int Count();
 [return:MarshalAs(UnmanagedType.IDispatch)] object Child([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Name([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Value([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Description([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Role([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object State([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Help([MarshalAs(UnmanagedType.Struct)]object child);
 int HelpTopic([MarshalAs(UnmanagedType.BStr)]out string file,[MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.BStr)] string Shortcut([MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Focus();
 [return:MarshalAs(UnmanagedType.Struct)] object Selection();
 [return:MarshalAs(UnmanagedType.BStr)] string DefaultAction([MarshalAs(UnmanagedType.Struct)]object child);
 void Select(int flags,[MarshalAs(UnmanagedType.Struct)]object child);
 void Location(out int left,out int top,out int width,out int height,[MarshalAs(UnmanagedType.Struct)]object child);
 [return:MarshalAs(UnmanagedType.Struct)] object Navigate(int direction,[MarshalAs(UnmanagedType.Struct)]object start);
 [return:MarshalAs(UnmanagedType.Struct)] object HitTest(int x,int y);
 void Invoke([MarshalAs(UnmanagedType.Struct)]object child);
 void SetName([MarshalAs(UnmanagedType.Struct)]object child,[MarshalAs(UnmanagedType.BStr)]string name);
 void SetValue([MarshalAs(UnmanagedType.Struct)]object child,[MarshalAs(UnmanagedType.BStr)]string value);
}

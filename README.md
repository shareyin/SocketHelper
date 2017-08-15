# SocketHelper
SocketHelper
使用方法：Clone到本地后，添加到自己项目中，修改命名空间即可（或者生成dll文件进行调用）
接口：TCP服务端，TCP客户端
初始化：InitSocket(string ipaddress,int port)
发送数据（字符串）SendStrData(string SendData)
发送数据（Hex数据）SendByteData(byte[] SendByteData int Slen)
接收数据。建议单独建立委托线程接收。
创建方法如下：
privite void Rec(SocketHelper.Sockets sks)
{
    this.Invoke(new ThreadStart(
    {
        if(sks.ex!=null)
        {
        //处理异常情况
        }
        else
        {
        //处理接收数据，
        //buff:sks.ResBuffer  len: sks.Offset
        }
    }))
}

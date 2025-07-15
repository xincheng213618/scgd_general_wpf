# ProjectARVRLite 

传入ProjectARVRInit,会初始化整个测试参数信息

成功后返回切图指令，切图拍照完成后执行ProjectARVRResult


```json
{ 
  "Version": "1.0", 
  "MsgID": "12345", 
  "EventName": "ProjectARVRInit", 
  "SerialNumber": "", 
  "Params": "" 
}
{"Code":0,"Msg":null,"Data":{"ARVRTestType":1},"Version":null,"MsgID":null,"EventName":"SwitchPG","SerialNumber":null}

{ 
  "Version": "1.0", 
  "MsgID": "12345", 
  "EventName": "SwitchPGCompleted", 
  "SerialNumber": "", 
  "Params": "" 
}
{"Code":0,"Msg":null,"Data":null,"Version":null,"MsgID":null,"EventName":"ProjectARVRResult","SerialNumber":null}
```


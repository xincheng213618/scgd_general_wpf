## 自动聚焦

发送

{
  "Version": "1.0",
  "EventName": "AutoFocus",
  "ServiceName": "camera/CMD/cs01",
  "CodeID": "e29b14429bc375b1",
  "Token": null,
  "SerialNumber": null,
  "MsgID": "500b7bf9-e4b5-4c4e-a8c2-7423d4bf129c",
  "params": {
    "params": {
      "tAutoFocusCfg": {
        "forwardparam": 2000.0,
        "curtailparam": 0.3,
        "curStep": 5000,
        "stopStep": 200,
        "minPosition": 80000,
        "maxPosition": 180000,
        "eEvaFunc": 1,
        "dMinValue": 0.0,
        "nTimeout": 30000
      }
    }
  }
}

接收

{
  "Version": null,
  "EventName": "AutoFocus",
  "ServiceName": "camera/CMD/cs01",
  "DeviceName": null,
  "CodeID": "e29b14429bc375b1",
  "SerialNumber": "",
  "Code": 0,
  "MsgID": "500b7bf9-e4b5-4c4e-a8c2-7423d4bf129c",
  "data": {

“nPos":2221

}
}
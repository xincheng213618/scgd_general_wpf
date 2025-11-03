# ColorVision API V1.0

## Command API 

### 概要

`ColorVision` [全局选项] {[输入文件选项] -i 输入文件路径} ... {[输出文件选项] 输出路径} ...

### Description

以下是一些使用 `ColorVision` 命令的简单示例：

1. **打开文件：**

   使用 `-i` 选项指定输入文件：

   ```
   ColorVision -i input.cvraw 
   ```

2. **启用调试模式：**

   使用 `-d` 选项启用调试模式：

   ```
   ColorVision -d
   ```

3. **快速启动：**

   使用 `-r` 选项快速重启：

   ```
   ColorVision -r
   ```

4. **通过重新编码媒体流将输入媒体文件转换为不同格式：**

   Use the `-e` option with `-q` (quiet)for conversion: 

   ```
   ColorVision -e input.cvraw -q 
   ```

5. **将输入媒体文件转换为指定格式：**

   使用 `-t` 选项指定格式（如 `tif`、`png`、`jpg`、`bmp`），使用 `-mx` 设置最大质量或压缩，并使用 `-o` 指定输出文件夹：

   ```
   ColorVision -e input.cvraw -q -t tif -mx 1
   ColorVision -e input.cvraw -q -t png -mx 3
   ColorVision -e input.cvraw -q -t jpg -mx 90
   ColorVision -e input.cvraw -q -t bmp 
   ```



## Socket API 

### 基础 URL

- **测试环境**:`http://127.0.0.1:6666`

### 标准未处理返回

- **响应:** `Unhandled Function Call`

### API Endpoints

#### 运行流程 (Flow)

- **端点格式:**

  ```
  Flow,{流程名}
  ```

- **示例:**

  ```
  Flow,poitest
  ```

- **成功响应:**

  ```
  Run {流程名}
  ```

- **错误响应:**

  ```
  Cant Find Flow {流程名}
  ```

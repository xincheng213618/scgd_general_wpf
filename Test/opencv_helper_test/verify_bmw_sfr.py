"""Read-only original-pixel BMW/SFR evidence. Requires numpy and opencv-python.
Example: python Test/opencv_helper_test/verify_bmw_sfr.py IMAGE OUTPUT --rois '[[1550,730,730,730]]'
ROI order is identity; never infer a five-point layout. Writes only under OUTPUT.
"""
import argparse, ctypes as C, hashlib, json, os
from pathlib import Path
import cv2
import numpy as np

class Image(C.Structure):
    _fields_=[('rows',C.c_int),('cols',C.c_int),('channels',C.c_int),('depth',C.c_int),('stride',C.c_int),('isDispose',C.c_bool),('pData',C.c_void_p)]
class Rect(C.Structure):
    _pack_=1
    _fields_=[('x',C.c_int),('y',C.c_int),('width',C.c_int),('height',C.c_int)]
def main():
    p=argparse.ArgumentParser(); p.add_argument('image'); p.add_argument('output'); p.add_argument('--rois',required=True); p.add_argument('--encoding',default='unknown',choices=['unknown','linear','srgb']); args=p.parse_args()
    root=Path(__file__).resolve().parents[2]
    runtime=os.add_dll_directory(str(root/'packages/opencv/x64/vc18/bin'))
    dllpath=root/'Native/opencv_helper/x64/Release/opencv_helper.dll'
    dll=C.CDLL(str(dllpath)); dll.M_LocateBmwTargetV1.argtypes=[Image,Rect,C.POINTER(C.c_void_p)]; dll.M_AnalyzeSfrV2.argtypes=[Image,Rect,C.c_char_p,C.POINTER(C.c_void_p)]; dll.FreeResult.argtypes=[C.c_void_p]
    source=Path(args.image); before=hashlib.sha256(source.read_bytes()).hexdigest()
    pixels=cv2.imdecode(np.fromfile(source,dtype=np.uint8),cv2.IMREAD_UNCHANGED)
    pixels=np.ascontiguousarray(pixels); channels=1 if pixels.ndim==2 else pixels.shape[2]
    image=Image(pixels.shape[0],pixels.shape[1],channels,pixels.dtype.itemsize*8,pixels.strides[0],True,pixels.ctypes.data)
    options={'encoding':args.encoding,'minimumContrast':.02,'minimumSnr':10,'maximumFitRms':.35}
    overlay=pixels.copy(); results=[]
    def call(fn,*parameters):
        ptr=C.c_void_p(); code=fn(*parameters,C.byref(ptr))
        if code<=0: raise RuntimeError(f'native error {code}')
        try: return json.loads(C.string_at(ptr,code-1))
        finally: dll.FreeResult(ptr)
    for index,coords in enumerate(json.loads(args.rois)):
        roi=Rect(*coords); result=call(dll.M_LocateBmwTargetV1,image,roi)
        result.update(id=f'ROI_{index+1}',searchRoi=coords)
        x,y,w,h=coords; cv2.rectangle(overlay,(x,y),(x+w,y+h),(0,0,255),3); cv2.putText(overlay,result['id'],(x,y-8),cv2.FONT_HERSHEY_SIMPLEX,1,(0,0,255),2)
        for edge in result['edges']:
            r=edge['roi']; edge['name']=['Left','Top','Right','Bottom'][edge['id']]
            if result['located'] and r['width'] and r['height']:
                edge['analysis']=call(dll.M_AnalyzeSfrV2,image,Rect(r['x'],r['y'],r['width'],r['height']),json.dumps(options).encode())
                x,y,w,h=(r[k] for k in ('x','y','width','height')); cv2.rectangle(overlay,(x,y),(x+w,y+h),(0,128,255),2)
                cv2.putText(overlay,edge['name'],(x,y-4),cv2.FONT_HERSHEY_SIMPLEX,.5,(0,128,255),1)
        results.append(result)
        print(result['id'],result['located'],result['reason'], [(e['name'],[(c['channel'],c['valid'],c['reason'],c['mtf50']) for c in e.get('analysis',{}).get('channels',[])]) for e in result['edges']])
    after=hashlib.sha256(source.read_bytes()).hexdigest(); assert before==after
    output=Path(args.output); output.mkdir(parents=True,exist_ok=True)
    (output/'results.json').write_text(json.dumps(dict(source=str(source),sourceSha256=before,dllSha256=hashlib.sha256(dllpath.read_bytes()).hexdigest(),sourceWidth=image.cols,sourceHeight=image.rows,options=options,results=results),indent=2),encoding='utf-8')
    cv2.imencode('.png',overlay)[1].tofile(output/'overlay.png')
if __name__=='__main__': main()

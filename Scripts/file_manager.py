import os
import requests
from tqdm import tqdm


class FileManager:
    def __init__(self, base_url):
        self.base_url = base_url

    def upload_file(self, file_path, folder_name):
        file_size = os.path.getsize(file_path)
        file_name = os.path.basename(file_path)
        upload_url = f'{self.base_url}/upload/{folder_name}/{file_name}'

        with open(file_path, 'rb') as f:
            with tqdm(total=file_size, unit='B', unit_scale=True, desc=file_name, ascii=True) as progress_bar:
                def read_in_chunks(file_object, chunk_size=1024):
                    while True:
                        data = file_object.read(chunk_size)
                        if not data:
                            break
                        yield data
                        progress_bar.update(len(data))

                response = requests.put(upload_url, data=read_in_chunks(f))

        if response.status_code == 201:
            print('File uploaded successfully')
        else:
            print('File upload failed:', response.text)

    def download_file(self, file_name, folder_name, save_path):
        download_url = f'{self.base_url}/download/{folder_name}/{file_name}'
        response = requests.get(download_url, stream=True)

        if response.status_code == 200:
            with open(save_path, 'wb') as f:
                for chunk in response.iter_content(chunk_size=1024):
                    f.write(chunk)
            print('File downloaded successfully')
        else:
            print('File download failed:', response.text)

    def delete_file(self, file_name, folder_name):
        delete_url = f'{self.base_url}/delete/{folder_name}/{file_name}'
        response = requests.delete(delete_url)

        if response.status_code == 200:
            print('File deleted successfully')
        else:
            print('File deletion failed:', response.text)

    def post_file(self, file_path, folder_name):
        file_name = os.path.basename(file_path)
        post_url = f'{self.base_url}/post/{folder_name}/{file_name}'

        with open(file_path, 'rb') as f:
            files = {'file': (file_name, f)}
            response = requests.post(post_url, files=files)

        if response.status_code == 201:
            print('File posted successfully')
        else:
            print('File post failed:', response.text)


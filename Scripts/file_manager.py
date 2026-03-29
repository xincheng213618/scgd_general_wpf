import os


from backend_client import (
    RemoteUploadSettings,
    DEFAULT_UPLOAD_URL,
    resolve_upload_base_url,
    resolve_upload_credentials,
    upload_file as authenticated_upload_file,
)


def _require_requests():
    try:
        import requests
    except ImportError as exc:
        raise RuntimeError("FileManager requires the requests package for this operation.") from exc
    return requests


class FileManager:
    def __init__(self, base_url=None, username=None, password=None):
        self.base_url = resolve_upload_base_url(base_url)
        self.username, self.password = resolve_upload_credentials(username, password)

    def upload_file(self, file_path, folder_name):
        if not self.username or not self.password:
            print(
                'Remote upload skipped: missing COLORVISION_UPLOAD_USERNAME/COLORVISION_UPLOAD_PASSWORD '
                'for the backend Basic Auth protected upload endpoint.'
            )
            return False

        settings = RemoteUploadSettings(
            base_url=self.base_url,
            folder_name=folder_name,
            username=self.username,
            password=self.password,
            enabled=True,
        )
        return authenticated_upload_file(file_path, settings)

    def download_file(self, file_name, folder_name, save_path):
        requests = _require_requests()
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
        requests = _require_requests()
        delete_url = f'{self.base_url}/delete/{folder_name}/{file_name}'
        response = requests.delete(delete_url)

        if response.status_code == 200:
            print('File deleted successfully')
        else:
            print('File deletion failed:', response.text)

    def post_file(self, file_path, folder_name):
        requests = _require_requests()
        file_name = os.path.basename(file_path)
        post_url = f'{self.base_url}/post/{folder_name}/{file_name}'

        with open(file_path, 'rb') as f:
            files = {'file': (file_name, f)}
            response = requests.post(post_url, files=files)

        if response.status_code == 201:
            print('File posted successfully')
        else:
            print('File post failed:', response.text)


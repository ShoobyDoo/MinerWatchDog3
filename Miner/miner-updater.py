# Miner Updater for Miner WatchDog 3
# --------------------------------
# Author : Doomlad
# Date   : 12/05/2021
# Edit   : 03/17/2022
# File   : miner-updater.py

from bs4 import BeautifulSoup
import requests
import sys
import zipfile
import argparse
import math
import shutil
import time
import os
import glob


__version__ = '1.4.1'

# init parser
parser = argparse.ArgumentParser(description="A Miner Updater for Miner WatchDog 3")
parser.add_argument("-v", "--version",
                    help="display updater version and exit", action="store_true")
parser.add_argument("-s", "--silent",
                    help="only return absolutely necessary information", action="store_true")
parser.add_argument("-i", "--install",
                    help="used with the --update flag to install downloaded miner zip", action="store_true")
parser.add_argument("-c", "--check",
                    help="gets the latest phoenix miner version", action="store_true")
parser.add_argument("-cl", "--changelog",
                    help="gets the latest phoenix miner versions changelog", action="store_true")
parser.add_argument("-u", "--update",
                    help="downloads the latest phoenix miner version", action="store_true")
parser.add_argument("miner", type=str, help="name of the miner (default: phoenixminer)", nargs="?")
# parser.add_argument("url", type=str, help="name of the miner", nargs="?")

args = parser.parse_args()
miner = args.miner


def get_version_internal():
    print("miner-updater {}".format(__version__))


def get_latest_version(silent_override: bool = False):
    if miner.lower() == "phoenixminer":
        latest_phoenix = "https://phoenixminer.org/download/latest/"
        page = requests.get(latest_phoenix)

        soup = BeautifulSoup(page.content, "html.parser")
        results = soup.find("div", {"class": "phe-ver"})
        version_long = results.text.split("SHA")[0].strip()
        version_short = version_long.split(" ")[-1]

        if args.silent or silent_override:
            if not silent_override: print(version_short)
            ver = version_short
        else:
            if not silent_override: print(version_long)
            ver = version_long

        return ver

    else:
        print("miner-updater: error: miner specified is not currently supported: {}".format(miner))


def get_latest_changelog():
    version = get_latest_version(silent_override=True)
    url = f"https://phoenixminer.org/download/{version}/#changelog"

    # creating requests object
    html = requests.get(url).content

    # creating soup object
    soup = BeautifulSoup(html, 'html.parser')

    # out var
    out = ''

    for ultag in soup.find_all('section', {'class': 'page__content'}):
        for litag in ultag.find_all('li'):
            ln = litag.text.strip()
            if ln == 'Changelog':
                out += f"{ln}\n\n"
            elif ln.endswith('.asc'):
                pass
            else:
                out += f"* {ln}\n\n"

    print(out)


def get_update():
    if miner.lower() == "phoenixminer":
        latest_phoenix = "https://phoenixminer.org/download/latest/"
        page = requests.get(latest_phoenix)
        soup = BeautifulSoup(page.content, "html.parser")
        div = soup.find('div', {'class': 'phe-button'})

        link = div.find(href=True)['href']
        file_name = "PhoenixMiner-latest.zip"
        miner_executable = "PhoenixMiner.exe"
        start = time.time()

        with open(file_name, "wb") as f:
            response = requests.get(link, stream=True)
            total_length = response.headers.get('content-length')
            in_mb = float(total_length) / math.pow(1024, 2)  # bytes to KB to MB bytes/1024*1024
            print(f"Downloading {file_name} (Size: {round(in_mb, 2)} MB)")

            if total_length is None or args.silent:  # no content length header
                f.write(response.content)
            else:
                dl = 0
                total_length = int(total_length)
                for data in response.iter_content(chunk_size=4096):
                    dl += len(data)
                    f.write(data)
                    done = int(50 * dl / total_length)
                    sys.stdout.write("\r[%s%s]" % ('=' * done, ' ' * (50-done)))
                    sys.stdout.flush()

            end = time.time()
            print(f"Download completed in {round(end - start, 2)} seconds.\n")

        f.close()

        if args.install:
            start = time.time()
            print(f"Updating...\n-> Extracting {file_name}")
            with zipfile.ZipFile(file_name, "r") as zip_ref:
                zip_ref.extractall(os.getcwd())
            
            os.remove(file_name)
            files = glob.glob("./*")
            for file in files:
                if file.endswith(".py") or file.startswith(miner_executable) or file.endswith("__"):
                    files.remove(file)

            # print(files)
            extracted_folder = files[-1]
            extracted_folder_name = extracted_folder.replace(".\\", "")
            #extracted_folder.replace('.\\', '')
            extracted_folder_contents = glob.glob(f"{extracted_folder}\*")
            print("-> Number of files: {}".format(len(extracted_folder_contents)))
            print("-> Deleting all other non-miner files (.bat, .txt, etc.)...")
            
            updated_miner = ""
            for contents in extracted_folder_contents:
                if contents.lower() == f"{extracted_folder}\{miner.lower()}.exe".lower():
                    # print(f"Miner located: {contents}")
                    updated_miner = contents
                else:
                    if contents.endswith("doc"):
                        for root, dirs, files in os.walk(contents):

                            for name in files:
                                    os.remove(os.path.join(root, name))
                                    # print("   -> Deleted: {}".format(os.path.join(root, name)))

                            for name in dirs:
                                    shutil.rmtree(os.path.join(root, name))
                                    # print("   -> Deleted: {}".format(os.path.join(root, name)))
                        os.rmdir(contents)
                    else:
                        os.remove(contents)
                    
            print("-> Updated miner: {}".format(updated_miner))
            print("-> Copying updated miner to root directory...")

            cut = os.getcwd() + f"\{extracted_folder_name}\\" + updated_miner.split("\\")[-1]
            # print(extracted_folder_name, cut)
            try:
                os.remove(miner_executable)
            except (FileNotFoundError, shutil.Error):
                pass    
            shutil.move(cut, os.getcwd())
            
            print("-> Cleaning up...")
            os.rmdir(extracted_folder_name)

            end = time.time()
            print(f"Update complete. (Time elapsed: {round(end - start, 2)} seconds.)")

    else:
        print("miner-updater: error: miner specified is not currently supported: {}".format(miner))


try:
    # check for --version or -V
    if args.version:
        get_version_internal()

    elif args.changelog:
        get_latest_changelog()
                
    elif args.check:
        get_latest_version()

    elif args.update:
        get_update()

except AttributeError as e:
    if args.miner == None:
        print("miner-updater: error: the following arguments are required: miner")

    elif args.path == None:
        print("miner-updater: error: the following arguments are required: path")

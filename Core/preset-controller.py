from pyautogui import press, typewrite, hotkey, keyDown, keyUp
import argparse
import sys
import traceback
import os

__version__ = '1.0.4'
__script__ = os.path.basename(__file__)

# init parser
parser = argparse.ArgumentParser(description="A keystroke emulation script Miner WatchDog 3")
parser.add_argument("-v", "--version", help="display preset controller version and exit", action="store_true")
parser.add_argument("keystroke", type=str, help="hotkey storke to be emulated (Ex. CTRL + C)", nargs="?")

args = parser.parse_args()

if args.version:
    print("preset-controller {}".format(__version__))
    sys.exit()

elif args.keystroke:
    print("Attempting to process keystroke: {}".format(args.keystroke))
    
    try:
        keys = args.keystroke.replace('+', '').replace("NumPad", "Num").split()
        
        for key in keys:
            keyDown(key.lower())

        for key in reversed(keys):
            keyUp(key.lower())
        
        print("No errors found! Keystroke has been processed.")

    except Exception:
        print("FATAL: {}".format(traceback.format_exc()))

else:
    print(__script__ + ": You must supply a keystroke argument surrounded by \"\"")

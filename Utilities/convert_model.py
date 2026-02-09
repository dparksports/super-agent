import os
import subprocess
import sys

def main():
    print("Gemma 3 27B ONNX Conversion Script")
    print("----------------------------------")
    print("This script helps you convert the Gemma 3 27B model to ONNX DirectML (Int4).")
    print("Prerequisites:")
    print("1. Python 3.10+")
    print("2. onnxruntime-genai-directml package installed")
    print("   pip install onnxruntime-genai-directml")
    print("3. Hugging Face Login (if required for gated models)")
    print("   huggingface-cli login")
    print("")

    # Define paths
    # We want to output to the app's Model directory relative to where this script usually lives
    # Assuming script is in <AppRoot>/Utilities/
    script_dir = os.path.dirname(os.path.abspath(__file__))
    app_root = os.path.dirname(script_dir)
    model_output_dir = os.path.join(app_root, "Model", "google", "gemma-3-27b-it-onnx-int4")

    # Ensure output directory exists (builder might create it, but good to be safe)
    if not os.path.exists(model_output_dir):
        os.makedirs(model_output_dir, exist_ok=True)

    print(f"Output Directory: {model_output_dir}")
    print("Starting conversion... This may take a while and requires significant RAM (50GB+ for 27B float16 weights).")
    
    # The command provided by user
    # python -m onnxruntime_genai.models.builder -m google/gemma-3-27b-it -e dml -p int4 -o ./gemma-3-onnx
    
    cmd = [
        sys.executable, "-m", "onnxruntime_genai.models.builder",
        "-m", "google/gemma-3-27b-it",
        "-e", "dml",
        "-p", "int4",
        "-o", model_output_dir
    ]

    print(f"Running command: {' '.join(cmd)}")
    
    try:
        subprocess.run(cmd, check=True)
        print("\nConversion Complete!")
        print(f"Model saved to: {model_output_dir}")
    except subprocess.CalledProcessError as e:
        print(f"\nError during conversion: {e}")
        print("Please ensure you have 'onnxruntime-genai' installed and enough RAM.")
        input("Press Enter to exit...")
        sys.exit(1)
    except Exception as ex:
        print(f"\nAn unexpected error occurred: {ex}")
        input("Press Enter to exit...")
        sys.exit(1)

    input("Press Enter to finish...")

if __name__ == "__main__":
    main()

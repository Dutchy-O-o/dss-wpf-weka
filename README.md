# DSSAppWpf — Weka-Based Decision Support System

A WPF (.NET Framework 4.8) decision support application that uses the
**Weka** machine learning library through **IKVM.NET**. Load an ARFF
dataset, run multiple classification algorithms in parallel, compare
results side by side (Accuracy, Kappa, F-Measure, etc.), and save or
load trained models.

## Features

- ARFF dataset loading (ships with `iris.arff` as a sample)
- One-click evaluation across **14 algorithms**:
  - **Baseline:** ZeroR
  - **Rules:** OneR
  - **Bayes:** Naive Bayes, BayesNet
  - **Functions:** Logistic Regression, Multilayer Perceptron (ANN), SMO (SVM)
  - **Lazy:** k-NN (k = 1, 3, 5, 7, 9)
  - **Trees:** J48 (C4.5), Random Tree, Random Forest, REPTree
- Automatic preprocessing (Discretize / Normalize + Dummy) per algorithm requirements
- Cross-validation and train/test split modes
- Save / load trained models as `.model` files

## Requirements

- Windows + Visual Studio 2019 / 2022 (or MSBuild 16+)
- .NET Framework **4.8**
- Target platform: **x86** (IKVM 32-bit)

## Setup

For licensing and file-size reasons, this repository does **not** include
the Weka and IKVM DLLs. After cloning, place the following files into
**`DSSAppWpf/libs/`**:

```
weka.dll
IKVM.Runtime.dll
IKVM.OpenJDK.Beans.dll
IKVM.OpenJDK.Charsets.dll
IKVM.OpenJDK.Corba.dll
IKVM.OpenJDK.Core.dll
IKVM.OpenJDK.Jdbc.dll
IKVM.OpenJDK.Management.dll
IKVM.OpenJDK.Media.dll
IKVM.OpenJDK.Misc.dll
IKVM.OpenJDK.Naming.dll
IKVM.OpenJDK.Remoting.dll
IKVM.OpenJDK.Security.dll
IKVM.OpenJDK.SwingAWT.dll
IKVM.OpenJDK.Text.dll
IKVM.OpenJDK.Tools.dll
IKVM.OpenJDK.Util.dll
IKVM.OpenJDK.XML.API.dll
IKVM.OpenJDK.XML.Bind.dll
IKVM.OpenJDK.XML.Crypto.dll
IKVM.OpenJDK.XML.Parse.dll
IKVM.OpenJDK.XML.Transform.dll
IKVM.OpenJDK.XML.WebServices.dll
IKVM.OpenJDK.XML.XPath.dll
```

How to obtain them:

1. **IKVM 8.x**: download `ikvm-<version>-bin.zip` from
   <https://github.com/ikvmnet/ikvm/releases> and copy the `IKVM.*.dll`
   files from its `bin/` folder.
2. **Weka 3.8.x**: download Weka from
   <https://www.cs.waikato.ac.nz/ml/weka/downloading.html>, then convert
   `weka.jar` into `weka.dll` with:

   ```
   ikvmc -target:library weka.jar
   ```

3. Drop the resulting `weka.dll` together with the IKVM DLLs into
   `DSSAppWpf/libs/`.

## Running

```
# In Visual Studio:
DSSAppWpf.sln  →  F5

# Or from the command line:
msbuild DSSAppWpf.sln /p:Configuration=Debug /p:Platform=x86
DSSAppWpf\bin\Debug\DSSAppWpf.exe
```

In the application:

1. Click **Load Dataset** and pick an `.arff` file (`iris.arff` is included).
2. Tick the algorithms you want to evaluate.
3. Choose an evaluation mode (Cross-Validation / Holdout).
4. Click **Run** to see results in a comparative table.
5. Optionally save the best model with **Save Model**.

> **Note:** The current UI strings are in Turkish. English localization is
> on the roadmap.

## Project Structure

```
DSSAppWpf/
├── App.xaml(.cs)              # WPF application entry point
├── MainWindow.xaml(.cs)       # Single-window UI
├── Themes/AppStyles.xaml      # Shared styles
├── Models/                    # POCO data models
│   ├── AlgorithmDescriptor.cs
│   ├── ClassifierResult.cs
│   ├── DatasetInfo.cs
│   └── EvaluationOptions.cs
├── Services/                  # Business logic layer
│   ├── ClassifierFactory.cs   # Algorithm catalog
│   ├── DatasetLoader.cs       # ARFF loading + preprocessing
│   ├── EvaluationEngine.cs    # CV / Holdout evaluation
│   └── ModelPersistence.cs    # Model save/load
├── libs/                      # (gitignored) Weka + IKVM DLLs
└── iris.arff                  # Sample dataset
```

## License

This project is distributed under the **GNU GPL v3** — see
[`LICENSE`](LICENSE).

> **Note:** Weka is licensed under GPL v3. Because this project depends on
> Weka, it is considered a derivative work and must remain GPL-v3
> compatible. Make sure you also comply with the licenses of the Weka and
> IKVM versions you bundle ([Weka GPL](https://www.gnu.org/licenses/gpl-3.0.html),
> [IKVM zlib/Apache](https://github.com/ikvmnet/ikvm/blob/main/LICENSE.md)).

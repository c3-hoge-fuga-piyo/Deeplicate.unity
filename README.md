# Deeplicate

[![Unity 5.4+](https://img.shields.io/badge/unity-5.4+-000.svg?style=flat-square&logo=unity)](https://unity3d.com/get-unity/download/archive)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg?style=flat-square)](LICENSE)
[![Releases](https://img.shields.io/github/release/c3-hoge-fuga-piyo/Deeplicate.unity.svg?style=flat-square)](https://github.com/c3-hoge-fuga-piyo/Deeplicate.unity/releases)


アセットをディレクトリ単位でコピーした際に、コピー元のアセットを参照してしまうお悩みを解決するヤーツ

## Installation

### Unity Package Manager (Git URL)

※Unity 2019.3.4f1 または Unity 2020.1.0a21 以降のみ

`Packages/manigest.json` の `dependencies` に `"dev.ohanaya.deeplicate": "https://github.com/c3-hoge-fuga-piyo/Deeplicate.unity.git?path=Assets/OhanaYa/Deeplicate"` を追加します。

参照: [Unity - Manual:  Installing from a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html)

### Asset Package

[Deeplicate.unity/releases](https://github.com/c3-hoge-fuga-piyo/Deeplicate.unity/releases) よりダウンロードした `Deeplicate.*.*.*.unitypackage` をプロジェクトにインポートします。

参照: [Unity - Manual:  Importing local Asset packages](https://docs.unity3d.com/Manual/AssetPackagesImport.html)

## Usage

1. コピーしたいディレクトリを選択し「Assets/Deeplicate」を実行する
2. 以上

![コピー元のアセットの参照関係](./images/1.png)
![Deeplicate実行](./images/2.png)
![コピーしたアセットの参照関係](./images/3.png)

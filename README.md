<p align="center">
  <img src="desc-images/DuoMix%20Banner%20Alt.png" alt="DuoMix Banner">
</p>

<h3 align="center">
  <b>Combine audio sources from your microphone input and external programs to output simultaneously through your mic in-game or voice applications.</b>
</h3>

<p align="center">
  <img src="desc-images/DuoMix%20Example.png" alt="DuoMix Example">
</p>

<i>Selected applications contribute their output to the mix; DuoMix does not inject microphone audio into those applications. Avoid selecting overlapping parent/child processes, which can duplicate audio. Add a restarted application again if its process ID changes. Game noise suppression or automatic gain may alter music and sound effects.</i>

## Install

1. [Download](https://github.com/Ixyrn/DuoMix/releases/tag/1.0.0) the Windows x64 portable ZIP from Releases and extract the entire folder to where ever you want.
2. Run `DuoMix.exe`. No separate .NET installation is required.
3. Choose your microphone and use **Add Program** to select audio sources.
4. Open **Game Microphone > Connection**. Use automatic detection or select both ends of your installed virtual cable.
5. Click **Start Mixing**, then select the displayed recording device in your game.

<p align="center">
  <b>If you like my work, you can</b>
  <a href="https://ko-fi.com/ixyrn">
    <img src="desc-images/Kofi%20icon.png" alt="Ko-fi" width="12">
    <b>Donate</b>
  </a>
  <b>to me</b>
</p>

The application executable is currently unsigned. The optional third-party driver retains its vendor signatures. This release has been checked on a development PC; clean-machine installation and every game/voice application have not been independently qualified.

## Virtual cable setup

A compatible virtual cable is required. DuoMix does not depend on a specific vendor.

- **Automatic** detects recognized, unambiguous cable endpoint pairs.
- **Choose endpoints** saves a playback destination and its matching recording endpoint. Missing saved devices do not silently fall back to another output.
- With **VB-CABLE**, send the mix to **CABLE Input** and select **CABLE Output** as the recording device in your game.
- With a mixer such as Voicemeter, select endpoints manually and configure the mixer's internal routing.

The portable package includes the original standard **VB-CABLE by VB-Audio** installer under "VirtualCable". Use **Install VB-CABLE** in the Connection panel if you need it. Installation is optional, requests administrator access, and may require a Windows restart. After restarting, refresh DuoMix.

VB-CABLE is donationware. All participations are welcome. Download, donate or pay for a license at [VB-Audio](https://vb-audio.com/Cable/). Its [distribution conditions](https://vb-audio.com/Services/licensing.htm) apply separately from DuoMix's MIT license; professional/institutional distribution may require purchased licenses. The paid A+B/C+D cables are not bundled.

**Rename** changes the recording endpoint's friendly name in Windows. Games may append the vendor name or need their device lists refreshed. It does not replace or modify the signed driver.

## Privacy and settings

DuoMix does not record audio to disk or send audio to an online service. Audio stays in memory and is routed locally. Idle monitoring does not feed the virtual cable. Preferences are stored in `%LOCALAPPDATA%\DuoMix\settings.json`; no user settings are shipped in release archives. Website buttons open your default browser only when clicked.

## License

DuoMix is licensed conditionally. Copyright (c) 2026 Ixyrn.

See [LICENSE](LICENSE) and [THIRD-PARTY.md](THIRD-PARTY.md) for dependency, font, runtime and driver licenses.

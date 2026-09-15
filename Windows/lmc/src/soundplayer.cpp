/****************************************************************************
**
** This file is part of LAN Messenger.
** 
** Copyright (c) 2010 - 2012 Qualia Digital Solutions.
** 
** Contact:  qualiatech@gmail.com
** 
** LAN Messenger is free software: you can redistribute it and/or modify
** it under the terms of the GNU General Public License as published by
** the Free Software Foundation, either version 3 of the License, or
** (at your option) any later version.
**
** LAN Messenger is distributed in the hope that it will be useful,
** but WITHOUT ANY WARRANTY; without even the implied warranty of
** MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
** GNU General Public License for more details.
**
** You should have received a copy of the GNU General Public License
** along with LAN Messenger.  If not, see <http://www.gnu.org/licenses/>.
**
****************************************************************************/

#include <QtGlobal>
#ifdef Q_OS_WIN
#include <Windows.h>
#include <QLibrary>
#else
//	QSound and QAudioDeviceInfo were both removed from QtMultimedia in
//	Qt6 (QSound had no direct fire-and-forget replacement - see play()
//	below; QAudioDeviceInfo's replacement is QMediaDevices).
#include <QSoundEffect>
#include <QMediaDevices>
#include <QAudioDevice>
#include <QUrl>
#endif
#include "soundplayer.h"

#ifdef Q_OS_WIN
typedef BOOL (WINAPI *sndPlaySoundFunc)(LPCSTR lpszSound, UINT fuSound);
static sndPlaySoundFunc sndPlaySoundFromDll = nullptr;
#endif

lmcSoundPlayer::lmcSoundPlayer(void) {
	pSettings = new lmcSettings();
	for(int index = 0; index < SE_Max; index++) {
		eventState[index] = Qt::Checked;
		sounds[index] = soundFile[index];
	}
    settingsChanged();
#ifdef Q_OS_WIN
    if(sndPlaySoundFromDll == nullptr) {
        QLibrary winmmDll("winmm");
        sndPlaySoundFromDll = (sndPlaySoundFunc) winmmDll.resolve("sndPlaySoundA");
    }
#endif
}

bool lmcSoundPlayer::isAvailable()
{
#ifdef Q_OS_WIN
    return sndPlaySoundFromDll != nullptr;
#else
    //	Fixed while migrating this line off the Qt6-removed
    //	QAudioDeviceInfo: the original returned .isEmpty() directly, i.e.
    //	"available" when there are NO output devices and "not available"
    //	when there are some - inverted from what the one caller
    //	(settingsdialog.cpp, which disables the Sounds settings group
    //	when !isAvailable()) actually wants. A pre-existing logic bug,
    //	not something this Qt6 migration introduced, but caught here.
    return !QMediaDevices::audioOutputs().isEmpty();
#endif
}

void lmcSoundPlayer::play(const QString &filename)
{
#ifdef Q_OS_WIN
    if(sndPlaySoundFromDll)
        sndPlaySoundFromDll(filename.toStdString().c_str(), SND_ASYNC);
#else
    //	QSound::play() was a fire-and-forget static call - QSoundEffect
    //	(its Qt6 replacement) needs a live QObject instance for the
    //	duration of playback, so this heap-allocates one and has it
    //	delete itself once playback actually stops (playingChanged()
    //	fires on both the start and stop transition, hence the
    //	isPlaying() check - deleting on the start transition would kill
    //	the sound before it plays).
    QSoundEffect* effect = new QSoundEffect();
    effect->setSource(QUrl::fromLocalFile(filename));
    effect->setLoopCount(1);
    QObject::connect(effect, &QSoundEffect::playingChanged, effect, [effect]() {
        if(!effect->isPlaying())
            effect->deleteLater();
    });
    effect->play();
#endif
}

void lmcSoundPlayer::play(SoundEvent event) {
	QString localStatus = pSettings->value(IDS_STATUS, IDS_STATUS_VAL).toString();
	if(!playSound || (localStatus == "Busy" && noBusySound) || (localStatus == "NoDisturb" && noDNDSound))
		return;

	if(!eventState[event])
		return;

    play(sounds[event]);
}

void lmcSoundPlayer::settingsChanged(void) {
	int size = qMin(pSettings->beginReadArray(IDS_SOUNDEVENTHDR), (int)SE_Max);
	for(int index = 0; index < size; index++) {
		pSettings->setArrayIndex(index);
		eventState[index] = pSettings->value(IDS_SOUNDEVENT, IDS_SOUNDEVENT_VAL).toInt();
	}
	pSettings->endArray();

	size = qMin(pSettings->beginReadArray(IDS_SOUNDFILEHDR), (int)SE_Max);
	for(int index = 0; index < size; index++) {
		pSettings->setArrayIndex(index);
		sounds[index] = pSettings->value(IDS_SOUNDFILE, soundFile[index]).toString();
	}
	pSettings->endArray();

	playSound = pSettings->value(IDS_SOUND, IDS_SOUND_VAL).toBool();
	noBusySound = pSettings->value(IDS_NOBUSYSOUND, IDS_NOBUSYSOUND_VAL).toBool();
	noDNDSound = pSettings->value(IDS_NODNDSOUND, IDS_NODNDSOUND_VAL).toBool();
}

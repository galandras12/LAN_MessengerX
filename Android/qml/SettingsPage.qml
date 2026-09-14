import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import QtQuick.Dialogs
import LanMessenger 1.0

Page {
    id: page
    title: qsTr("Settings")

    //	localAvatarPath itself never changes (see MessengerBridge's class
    //	comment - it's a fixed path, only its file content does), so bump
    //	this after every setAvatar() call to force the Image source below
    //	to re-fetch instead of showing a cached copy of the old picture.
    property int avatarVersion: 0

    FileDialog {
        id: avatarDialog
        title: qsTr("Choose a profile picture")
        nameFilters: [qsTr("Images (*.png *.jpg *.jpeg)")]
        onAccepted: {
            messenger.setAvatar(selectedFile)
            page.avatarVersion++
        }
    }

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 4
            ToolButton {
                text: "←"
                onClicked: StackView.view.pop()
            }
            Label {
                text: page.title
                font.pixelSize: 18
                Layout.fillWidth: true
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        anchors.margins: 16
        spacing: 20

        ColumnLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 6

            Rectangle {
                id: avatarPreview
                Layout.alignment: Qt.AlignHCenter
                width: 96
                height: 96
                radius: 48
                clip: true
                color: Material.dividerColor

                Image {
                    //	QQuickImage only re-fetches when the source URL
                    //	string itself changes, not merely when its binding
                    //	re-evaluates - since localAvatarPath is a fixed
                    //	path that only changes *content* (see
                    //	MessengerBridge's class comment), a "#v=N" fragment
                    //	(stripped by QUrl before it ever reaches the file
                    //	path) is appended so picking a new picture actually
                    //	produces a different source string and forces a
                    //	reload instead of showing the previously cached image.
                    anchors.fill: parent
                    source: messenger.localAvatarPath.length > 0
                            ? "file://" + messenger.localAvatarPath + "#v=" + page.avatarVersion
                            : ""
                    fillMode: Image.PreserveAspectCrop
                    asynchronous: true
                    cache: false
                }

                Label {
                    anchors.centerIn: parent
                    visible: messenger.localAvatarPath.length === 0
                    text: messenger.localUserName.length > 0 ? messenger.localUserName.charAt(0).toUpperCase() : "?"
                    font.pixelSize: 32
                    font.bold: true
                }

                MouseArea {
                    anchors.fill: parent
                    onClicked: avatarDialog.open()
                }
            }

            Label {
                Layout.alignment: Qt.AlignHCenter
                text: qsTr("Tap to change picture")
                font.pixelSize: 11
                opacity: 0.6
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 4

            Label {
                text: qsTr("Display name")
                font.pixelSize: 13
                opacity: 0.6
            }
            TextField {
                id: nameField
                Layout.fillWidth: true
                text: messenger.localUserName
                onEditingFinished: messenger.setLocalName(text)
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 4

            Label {
                text: qsTr("Status")
                font.pixelSize: 13
                opacity: 0.6
            }
            ComboBox {
                id: statusCombo
                Layout.fillWidth: true

                //	messenger.statusCodes()/statusLabels() are same-order
                //	parallel lists (Core's statusCode[]/lmcStrings::
                //	statusDesc()) - zipped into {code, label} objects here
                //	since ComboBox wants one model, not two.
                property var codes: messenger.statusCodes()
                model: {
                    var labels = messenger.statusLabels()
                    var result = []
                    for(var i = 0; i < codes.length; i++)
                        result.push({"code": codes[i], "label": labels[i]})
                    return result
                }
                textRole: "label"
                valueRole: "code"

                Component.onCompleted: currentIndex = codes.indexOf(messenger.localStatus)

                onActivated: messenger.setLocalStatus(currentValue)
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 4

            Label {
                text: qsTr("Note")
                font.pixelSize: 13
                opacity: 0.6
            }
            TextField {
                id: noteField
                Layout.fillWidth: true
                placeholderText: qsTr("What are you up to?")
                text: messenger.localNote
                onEditingFinished: messenger.setLocalNote(text)
            }
        }

        ColumnLayout {
            Layout.fillWidth: true
            spacing: 4

            Label {
                text: qsTr("Privacy")
                font.pixelSize: 13
                opacity: 0.6
            }
            Switch {
                id: historySwitch
                text: qsTr("Save message history")
                checked: messenger.historyEnabled
                onToggled: messenger.historyEnabled = checked
            }
        }

        Label {
            text: qsTr("Your device/network user id: %1").arg(messenger.localUserId)
            font.pixelSize: 11
            opacity: 0.5
            wrapMode: Text.Wrap
            Layout.fillWidth: true
        }

        Item { Layout.fillHeight: true }
    }

    //	Keeps the fields in sync if the profile changes from elsewhere
    //	(there is no other UI path to do so yet, but this avoids the
    //	fields silently going stale if one is ever added).
    Connections {
        target: messenger
        function onLocalProfileChanged() {
            if(!nameField.activeFocus)
                nameField.text = messenger.localUserName
            if(!noteField.activeFocus)
                noteField.text = messenger.localNote
            statusCombo.currentIndex = statusCombo.codes.indexOf(messenger.localStatus)
        }
    }
}

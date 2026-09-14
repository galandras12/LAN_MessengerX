import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page
    title: qsTr("Settings")

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

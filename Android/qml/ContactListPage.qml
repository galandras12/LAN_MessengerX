import QtQuick
import QtQuick.Controls
import QtQuick.Controls.Material
import QtQuick.Layouts
import LanMessenger 1.0

Page {
    id: page

    signal contactSelected(string userId, string name)

    header: ToolBar {
        Material.foreground: "white"
        RowLayout {
            anchors.fill: parent
            anchors.leftMargin: 16
            Label {
                text: messenger.localUserName.length > 0
                      ? qsTr("LAN Messenger X — %1").arg(messenger.localUserName)
                      : qsTr("LAN Messenger X")
                font.pixelSize: 18
                elide: Text.ElideRight
                Layout.fillWidth: true
            }
            Label {
                text: messenger.connected ? qsTr("online") : qsTr("connecting…")
                opacity: 0.8
                Layout.rightMargin: 16
            }
        }
    }

    ListView {
        id: contactList
        anchors.fill: parent
        model: messenger.contacts
        clip: true

        delegate: ItemDelegate {
            width: ListView.view.width
            height: 64

            contentItem: RowLayout {
                spacing: 12

                Rectangle {
                    Layout.preferredWidth: 40
                    Layout.preferredHeight: 40
                    radius: 20
                    color: (status === "chat") ? "#2e7d32"
                         : (status === "away" || status === "brb") ? "#f9a825"
                         : (status === "busy" || status === "dnd") ? "#c62828"
                         : "#9e9e9e"

                    Text {
                        anchors.centerIn: parent
                        text: name.length > 0 ? name.charAt(0).toUpperCase() : "?"
                        color: "white"
                        font.bold: true
                    }
                }

                ColumnLayout {
                    Layout.fillWidth: true
                    spacing: 2

                    Text {
                        text: name
                        font.pixelSize: 16
                        elide: Text.ElideRight
                        Layout.fillWidth: true
                    }
                    Text {
                        text: note.length > 0 ? note : status
                        font.pixelSize: 12
                        opacity: 0.6
                        elide: Text.ElideRight
                        Layout.fillWidth: true
                    }
                }
            }

            onClicked: page.contactSelected(userId, name)
        }
    }

    Label {
        anchors.centerIn: parent
        visible: contactList.count === 0
        text: qsTr("No one else is on the network yet…")
        opacity: 0.6
    }
}

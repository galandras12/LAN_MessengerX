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
            }
            ToolButton {
                text: "👥+"
                ToolTip.visible: hovered
                ToolTip.text: qsTr("New group chat")
                onClicked: StackView.view.push(Qt.resolvedUrl("NewGroupChatPage.qml"))
            }
        }
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 0

        Label {
            visible: roomList.count > 0
            text: qsTr("Group Chats")
            font.bold: true
            font.pixelSize: 13
            opacity: 0.6
            Layout.fillWidth: true
            Layout.leftMargin: 16
            Layout.topMargin: 8
        }

        //	Compact, non-scrolling list of active group chat rooms - kept
        //	small (Layout.preferredHeight bound to its own content) rather
        //	than fillHeight, since the main contact list below is the
        //	primary content of this page.
        ListView {
            id: roomList
            visible: count > 0
            Layout.fillWidth: true
            Layout.preferredHeight: Math.min(contentHeight, page.height * 0.35)
            interactive: contentHeight > height
            clip: true
            model: messenger.rooms

            delegate: ItemDelegate {
                width: ListView.view.width
                height: 56

                contentItem: RowLayout {
                    spacing: 12

                    Rectangle {
                        Layout.preferredWidth: 36
                        Layout.preferredHeight: 36
                        radius: 18
                        color: Material.accentColor
                        Text {
                            anchors.centerIn: parent
                            text: "👥"
                            font.pixelSize: 16
                        }
                    }

                    ColumnLayout {
                        Layout.fillWidth: true
                        spacing: 2
                        Text {
                            text: title
                            font.pixelSize: 15
                            elide: Text.ElideRight
                            Layout.fillWidth: true
                        }
                        Text {
                            text: qsTr("%1 participants").arg(participantCount)
                            font.pixelSize: 11
                            opacity: 0.6
                        }
                    }
                }

                onClicked: StackView.view.push(Qt.resolvedUrl("GroupChatPage.qml"), {"threadId": threadId})
            }
        }

        Label {
            visible: roomList.count > 0
            text: qsTr("Contacts")
            font.bold: true
            font.pixelSize: 13
            opacity: 0.6
            Layout.fillWidth: true
            Layout.leftMargin: 16
            Layout.topMargin: 8
        }

        ListView {
            id: contactList
            Layout.fillWidth: true
            Layout.fillHeight: true
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

            Label {
                anchors.centerIn: parent
                visible: contactList.count === 0
                text: qsTr("No one else is on the network yet…")
                opacity: 0.6
            }
        }
    }
}
